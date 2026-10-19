<script setup>
import { ref, computed, nextTick, onMounted, onUnmounted, watch } from 'vue';
import { API, createPoller } from '../../api.js';
import { pickImageFile } from '../../composables/pickImage.js';
import TabletChat from './TabletChat.vue';
import TabletClues from './TabletClues.vue';
import TabletExportDialog from './TabletExportDialog.vue';
import TabletPhotoConfirm from './TabletPhotoConfirm.vue';
import TabletPhotoViewer from './TabletPhotoViewer.vue';
import { t } from '../../i18n/index.js';
import { orderClues, roundNumbering, formatChatText, formatClueText, fileStampOf } from './tabletFormat.js';
import { renderChatParts, renderClueParts, downloadBlob, downloadText, fileToJpegBase64, imageBlobToPng } from './tabletImage.js';

/**
 * 平板电脑（实验性）：左侧聊天、右侧线索。数据由后端在游戏内采集，前端每 0.8 秒增量拉取。
 * 发言只在大厅与学级裁判可用；照片只在学级裁判的讨论阶段可用，并受照片冷却约束（均由后端与游戏双重校验）。
 */

const POLL_MS = 800;
const PHOTO_LIMIT = 163840;
/** 待确认照片在查看器中的占位键，不与任何聊天照片的 photoKey 冲突。 */
const PENDING_KEY = '__pending__';
const LINE_CAP = 1000;
const CLEAR_TEXT = {
  chat: '清空本地聊天记录（含照片）？线索保留，游戏内容不受影响。',
  clues: '清空本地线索？聊天记录保留，游戏内容不受影响。',
  all: '清空本地全部记录（聊天、线索、照片）？游戏内容不受影响。',
};

const enabled = ref(null);
const epoch = ref(null);
const cursor = ref(0);
const stateCode = ref('');
const stateLabel = ref('');
const canType = ref(false);
const typeReason = ref('');
const lines = ref([]);
const clues = ref([]);
const error = ref('');
const toast = ref({ text: '', error: false });
let toastTimer = null;

const chatEl = ref(null);
const rootEl = ref(null);
let stickBottom = true;

const input = ref('');
const sending = ref(false);
const photoBusy = ref(false);
const cooldownUntil = ref(0);
const tick = ref(Date.now());
const menu = ref('');
const exportDialog = ref(null);
const confirmScope = ref('');
/** 待确认发送的照片：压缩后的 JPEG（base64）与预览地址，确认前不会发出。 */
const pendingPhoto = ref(null);
/** 照片查看器：{ key, pending }。pending 为 true 时查看的是待确认照片。 */
const viewer = ref(null);

const clueList = computed(() => orderClues(clues.value));
/** 局编号由聊天与线索共同计算，同一局在两边的编号一致。 */
const roundNo = computed(() => roundNumbering([...lines.value, ...clues.value]));
const canSendPhoto = computed(() => stateCode.value === 'Trial' && canType.value);
const photoWait = computed(() => Math.max(0, Math.ceil((cooldownUntil.value - tick.value) / 1000)));
/** 聊天中的全部照片，供查看器左右切换（与聊天顺序一致）。 */
const photoItems = computed(() => lines.value
  .filter((l) => l.kind === 'photo')
  .map((l) => ({ key: l.photoKey, src: API.tabletPhotoUrl(l.photoKey), name: l.name, time: l.time, line: l })));
const viewerItems = computed(() => (viewer.value?.pending
  ? [{ key: PENDING_KEY, src: pendingPhoto.value?.url || '', name: '', time: '', line: null }]
  : photoItems.value));
const viewerKey = computed(() => (viewer.value?.pending ? PENDING_KEY : viewer.value?.key || ''));
watch(photoItems, (items) => {
  // 查看的照片因聊天容量被挤掉时，关闭查看器，避免显示空白
  if (viewer.value && !viewer.value.pending && !items.some((p) => p.key === viewer.value.key)) viewer.value = null;
});
const photoButtonText = computed(() => {
  if (photoBusy.value) return '处理中…';
  if (photoWait.value > 0) return `图片 ${photoWait.value}s`;
  return '图片';
});
const photoTitle = computed(() => {
  if (!canSendPhoto.value) return '仅学级裁判的讨论阶段可以发送照片';
  if (photoWait.value > 0) return `照片冷却中，${photoWait.value} 秒后可再发（与游戏内相同，每 5 秒一张）`;
  return '发送照片（也可直接粘贴剪贴板中的图片）';
});
const placeholder = computed(() => (canType.value ? '输入消息，回车发送' : (typeReason.value || '当前阶段不可发言')));
const stateText = computed(() => {
  if (!stateCode.value) return '等待阶段同步';
  return stateLabel.value || stateCode.value;
});
const exportItems = computed(() => (exportDialog.value?.kind === 'clue' ? clueList.value : lines.value));

function flash(text, isError = false) {
  toast.value = { text, error: isError };
  clearTimeout(toastTimer);
  if (text) toastTimer = setTimeout(() => { toast.value = { text: '', error: false }; }, 2600);
}

function nearBottom(el) {
  return el.scrollHeight - el.scrollTop - el.clientHeight < 60;
}

async function scrollToBottom() {
  await nextTick();
  if (chatEl.value) chatEl.value.scrollTop = chatEl.value.scrollHeight;
}

function onChatScroll() {
  if (chatEl.value) stickBottom = nearBottom(chatEl.value);
}

async function poll() {
  const j = await API.tabletState(cursor.value);
  if (j.unauthorized) return;
  if (!j.ok) {
    error.value = j.error || '读取失败';
    return;
  }
  error.value = '';
  const d = j.data || {};
  enabled.value = !!d.enabled;
  stateCode.value = d.state?.code ?? '';
  stateLabel.value = d.state?.label ?? '';
  canType.value = !!d.canType;
  typeReason.value = d.typeReason || '';
  clues.value = d.clues || [];
  const cool = Number(d.photoCooldownMs) || 0;
  cooldownUntil.value = cool > 0 ? Date.now() + cool : 0;

  // 存储被清理或后端重启（代数变化、序号回退）：丢弃本地列表，从头同步一次
  if ((epoch.value !== null && d.epoch !== epoch.value) || d.seq < cursor.value) {
    epoch.value = d.epoch;
    lines.value = [];
    cursor.value = 0;
    return poll();
  }
  epoch.value = d.epoch;

  const incoming = d.lines || [];
  if (incoming.length) {
    const wasAtBottom = chatEl.value ? nearBottom(chatEl.value) : true;
    const merged = lines.value.concat(incoming);
    lines.value = merged.length > LINE_CAP ? merged.slice(merged.length - LINE_CAP) : merged;
    if (wasAtBottom || stickBottom) scrollToBottom();
  }
  cursor.value = d.seq;
}

const poller = createPoller(poll, POLL_MS);
let clock = null;

async function send() {
  const text = input.value.trim();
  if (!text || !canType.value || sending.value) return;
  sending.value = true;
  const j = await API.tabletSend(text);
  sending.value = false;
  if (j.unauthorized) return;
  if (!j.ok) {
    flash(j.data?.message || j.error || '发送失败', true);
    return;
  }
  input.value = '';
  stickBottom = true;
  poll();
}

function onEnter(e) {
  if (e.isComposing) return;
  e.preventDefault();
  send();
}

/**
 * 取得一张待发送的照片：压缩为发送时的同一份 JPEG，弹出确认层。
 * 真正发送只在用户确认后发生（见 confirmPhoto），避免误触粘贴就直接发出。
 */
async function preparePhoto(file) {
  if (pendingPhoto.value) {
    flash(t('tablet.photo.pendingBlocked'), true);
    return;
  }
  if (photoBusy.value) {
    flash(t('tablet.photo.processing'), true);
    return;
  }
  if (!canSendPhoto.value) {
    flash('仅学级裁判的讨论阶段可以发送照片', true);
    return;
  }
  if (photoWait.value > 0) {
    flash(`照片冷却中，${photoWait.value} 秒后可再发`, true);
    return;
  }
  photoBusy.value = true;
  try {
    const data = await fileToJpegBase64(file, PHOTO_LIMIT);
    pendingPhoto.value = { data, url: `data:image/jpeg;base64,${data}` };
  } catch (e) {
    flash(e.message || '照片处理失败', true);
  } finally {
    photoBusy.value = false;
  }
}

/** 取消待发送的照片（取消按钮、Esc 或点击遮罩）。若正在查看它，一并关闭查看器。 */
function cancelPhoto() {
  pendingPhoto.value = null;
  if (viewer.value?.pending) viewer.value = null;
}

/**
 * 用户确认后才真正发送。确认时若已不可发（阶段变化），取消并提示；
 * 仍在冷却则保留确认层，等冷却结束再点「发送」。发送结束（成功或失败）都关闭确认层。
 */
async function confirmPhoto() {
  const p = pendingPhoto.value;
  if (!p || photoBusy.value) return;
  if (!canSendPhoto.value) {
    flash('当前阶段不能发送照片', true);
    cancelPhoto();
    return;
  }
  if (photoWait.value > 0) {
    flash(`照片冷却中，${photoWait.value} 秒后可再发`, true);
    return;
  }
  photoBusy.value = true;
  try {
    const j = await API.tabletPhoto(p.data);
    if (j.unauthorized) return;
    if (!j.ok) {
      flash(j.data?.message || j.error || '照片发送失败', true);
      return;
    }
    flash(j.data?.message || '照片已发送');
    stickBottom = true;
    poll();
  } catch (e) {
    flash(e.message || '照片发送失败', true);
  } finally {
    photoBusy.value = false;
    cancelPhoto();
  }
}

async function pickPhoto() {
  if (!canSendPhoto.value || photoBusy.value) return;
  if (pendingPhoto.value) {
    flash(t('tablet.photo.pendingBlocked'), true);
    return;
  }
  if (photoWait.value > 0) {
    flash(`照片冷却中，${photoWait.value} 秒后可再发`, true);
    return;
  }
  const file = await pickImageFile('image/*');
  if (file) preparePhoto(file);
}

/**
 * 粘贴剪贴板中的图片：先进入发送确认，不直接发送。纯文字粘贴保持原样，交给输入框处理。
 * 查看器打开时不响应粘贴。
 */
function onPaste(e) {
  const data = e.clipboardData;
  if (!data) return;
  const item = [...data.items].find((i) => i.kind === 'file' && i.type.startsWith('image/'));
  if (!item || viewer.value) return;
  if (data.getData('text/plain')) return;
  e.preventDefault();
  const file = item.getAsFile();
  if (file) preparePhoto(file);
}

function openViewer(l) {
  viewer.value = { key: l.photoKey, pending: false };
}
function openPendingViewer() {
  if (pendingPhoto.value) viewer.value = { key: PENDING_KEY, pending: true };
}
function closeViewer() {
  viewer.value = null;
}
function onViewerSelect(key) {
  viewer.value = { key, pending: false };
}

/** 点击面板空白处时把焦点移到根节点，使 Ctrl+V 的粘贴事件落在平板上。输入框与按钮保持原有焦点行为。 */
function focusRoot(e) {
  if (e.target.closest?.('input, textarea, select, button, a')) return;
  rootEl.value?.focus({ preventScroll: true });
}

async function copyPhoto(l) {
  if (!navigator.clipboard || typeof ClipboardItem === 'undefined' || !window.isSecureContext) {
    flash('当前不是安全连接（需 localhost 或 HTTPS），无法写入剪贴板，请使用「另存为」', true);
    return;
  }
  try {
    const blob = await API.tabletPhotoBlob(l.photoKey);
    const png = await imageBlobToPng(blob);
    await navigator.clipboard.write([new ClipboardItem({ 'image/png': png })]);
    flash('图片已复制');
  } catch (e) {
    flash(`复制失败：${e.message || e}`, true);
  }
}

async function savePhoto(l) {
  try {
    const blob = await API.tabletPhotoBlob(l.photoKey);
    downloadBlob(blob, `照片_${fileStampOf(new Date())}_${l.photoKey}.jpg`);
    flash('已开始保存');
  } catch (e) {
    flash(`保存失败：${e.message || e}`, true);
  }
}

function toggleMenu(name) {
  menu.value = menu.value === name ? '' : name;
}

function openExport(kind, format) {
  menu.value = '';
  exportDialog.value = { kind, format };
}

async function runExport(selected) {
  const { kind, format } = exportDialog.value;
  exportDialog.value = null;
  if (!selected.length) return;
  const title = kind === 'chat' ? '聊天记录' : '线索';
  const stamp = fileStampOf(new Date());
  try {
    if (format === 'text') {
      const text = kind === 'chat'
        ? formatChatText(selected, new Date(), roundNo.value)
        : formatClueText(selected, new Date(), roundNo.value);
      downloadText(text, `${title}_${stamp}.txt`);
      flash(`已导出 ${selected.length} 条`);
      return;
    }
    flash('正在生成图片…');
    const blobs = kind === 'chat'
      ? await renderChatParts(selected, title, roundNo.value)
      : await renderClueParts(selected, title, roundNo.value);
    blobs.forEach((blob, i) => {
      const part = blobs.length > 1 ? `_${i + 1}` : '';
      downloadBlob(blob, `${title}_${stamp}${part}.png`);
    });
    flash(blobs.length > 1 ? `已导出 ${blobs.length} 张图片` : '已导出图片');
  } catch (e) {
    flash(`导出失败：${e.message || e}`, true);
  }
}

function askClear(scope) {
  menu.value = '';
  confirmScope.value = scope;
}

async function doClear() {
  const scope = confirmScope.value;
  confirmScope.value = '';
  if (!scope) return;
  const j = await API.tabletClear(scope);
  if (j.unauthorized) return;
  if (!j.ok) {
    flash(j.data?.message || j.error || '清理失败', true);
    return;
  }
  if (scope !== 'clues') {
    lines.value = [];
    cursor.value = 0;
  }
  if (scope !== 'chat') clues.value = [];
  flash(j.data?.message || '已清理');
  poll();
}

onMounted(() => {
  poller.start();
  clock = setInterval(() => { tick.value = Date.now(); }, 250);
});
onUnmounted(() => {
  poller.stop();
  clearInterval(clock);
});
</script>

<template>
  <div ref="rootEl" class="tablet-app" tabindex="-1" @mousedown="focusRoot" @paste="onPaste">
    <header class="top">
      <div class="state" :class="{ on: canType }">
        <span class="dot"></span>
        <span class="state-name">{{ stateText }}</span>
        <span class="flag">{{ canType ? '可发言' : '不可发言' }}</span>
      </div>
      <div class="actions">
        <div class="menu-wrap">
          <button class="tb-btn" @click="toggleMenu('chat')">导出聊天记录 ▾</button>
          <div v-if="menu === 'chat'" class="menu">
            <button @click="openExport('chat', 'text')">导出文本</button>
            <button @click="openExport('chat', 'image')">导出图片</button>
          </div>
        </div>
        <div class="menu-wrap">
          <button class="tb-btn" @click="toggleMenu('clue')">导出线索 ▾</button>
          <div v-if="menu === 'clue'" class="menu">
            <button @click="openExport('clue', 'text')">导出文本</button>
            <button @click="openExport('clue', 'image')">导出图片</button>
          </div>
        </div>
        <div class="menu-wrap">
          <button class="tb-btn danger" @click="toggleMenu('clear')">清理 ▾</button>
          <div v-if="menu === 'clear'" class="menu">
            <button class="danger" @click="askClear('chat')">清理聊天记录</button>
            <button class="danger" @click="askClear('clues')">清理线索</button>
            <button class="danger" @click="askClear('all')">清理所有</button>
          </div>
        </div>
      </div>
    </header>

    <div v-if="confirmScope" class="confirm">
      <span>{{ CLEAR_TEXT[confirmScope] }}</span>
      <button class="tb-btn" @click="confirmScope = ''">取消</button>
      <button class="tb-btn danger" @click="doClear">确认清理</button>
    </div>

    <div v-if="enabled === false" class="notice">
      「平板电脑（实验性）」未启用。请在 DT 配置中打开该功能后再使用。
    </div>
    <div v-else-if="error" class="notice err">读取失败：{{ error }}</div>

    <div class="panes">
      <section class="pane chat-pane">
        <div ref="chatEl" class="chat-scroll" @scroll="onChatScroll">
          <TabletChat :lines="lines" :numbering="roundNo" @copy-photo="copyPhoto" @save-photo="savePhoto" @open-photo="openViewer" />
          <div v-if="!lines.length" class="empty">暂无聊天记录</div>
        </div>
        <div class="input-bar">
          <button
            class="tb-btn"
            :disabled="!canSendPhoto || photoBusy || photoWait > 0"
            :title="photoTitle"
            @click="pickPhoto"
          >{{ photoButtonText }}</button>
          <input
            v-model="input"
            class="chat-input"
            type="text"
            :disabled="!canType"
            :placeholder="placeholder"
            @keydown.enter="onEnter"
          />
          <button class="tb-btn primary" :disabled="!canType || !input.trim() || sending" @click="send">发送</button>
        </div>
      </section>

      <section class="pane clue-pane">
        <div class="clue-scroll">
          <TabletClues :clues="clueList" :numbering="roundNo" />
        </div>
      </section>
    </div>

    <div v-if="toast.text" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <TabletPhotoConfirm
      v-if="pendingPhoto"
      :src="pendingPhoto.url"
      :can-send="canSendPhoto"
      :wait-sec="photoWait"
      :busy="photoBusy"
      @cancel="cancelPhoto"
      @confirm="confirmPhoto"
      @zoom="openPendingViewer"
    />
    <TabletPhotoViewer
      v-if="viewer"
      :photos="viewerItems"
      :current-key="viewerKey"
      @select="onViewerSelect"
      @close="closeViewer"
      @copy="copyPhoto"
      @save="savePhoto"
    />
    <TabletExportDialog
      v-if="exportDialog"
      :kind="exportDialog.kind"
      :format="exportDialog.format"
      :items="exportItems"
      :numbering="roundNo"
      @cancel="exportDialog = null"
      @confirm="runExport"
    />
  </div>
</template>

<style scoped>
.tablet-app { position: relative; display: flex; flex-direction: column; gap: 8px; height: 100%; box-sizing: border-box; padding: 10px 12px; background: var(--surface-0); color: var(--text-1); overflow: hidden; outline: none; }
.top { display: flex; align-items: center; justify-content: space-between; gap: 10px; flex-wrap: wrap; }
.state { display: inline-flex; align-items: center; gap: 7px; font-size: 12px; color: var(--text-1); border: 1px solid var(--line); border-radius: 999px; padding: 4px 11px; }
.state .dot { width: 7px; height: 7px; border-radius: 50%; background: var(--text-2); }
.state.on .dot { background: var(--accent-green); box-shadow: 0 0 6px rgba(71, 212, 185, 0.6); }
.state-name { color: var(--text-0); font-weight: 500; }
.flag { font-size: 10.5px; color: var(--text-2); }
.state.on .flag { color: var(--accent-green); }
.actions { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.menu-wrap { position: relative; }
.menu { position: absolute; right: 0; top: calc(100% + 4px); z-index: 20; display: flex; flex-direction: column; min-width: 120px; background: var(--surface-2); border: 1px solid var(--line-strong); border-radius: var(--radius-sm); box-shadow: var(--menu-shadow); overflow: hidden; }
.menu button { text-align: left; background: none; border: none; color: var(--text-1); font-size: 12px; padding: 7px 12px; cursor: pointer; white-space: nowrap; }
.menu button:hover { background: rgba(140, 180, 255, 0.08); color: var(--text-0); }
.menu button.danger { color: var(--accent-red); }
.tb-btn { background: var(--surface-2); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-1); font-size: 11.5px; padding: 6px 10px; cursor: pointer; white-space: nowrap; }
.tb-btn:disabled { opacity: 0.45; cursor: default; }
.tb-btn.primary { background: var(--accent-blue-btn); border-color: transparent; color: #fff; }
.tb-btn.danger { color: var(--accent-red); }
.confirm { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; font-size: 12px; color: var(--text-1); background: var(--surface-2); border: 1px solid var(--accent-red); border-radius: var(--radius-sm); padding: 7px 10px; }
.confirm span { flex: 1; min-width: 180px; }
.notice { font-size: 12px; color: var(--accent-amber); border: 1px dashed rgba(245, 197, 66, 0.4); border-radius: var(--radius-sm); padding: 6px 10px; }
.notice.err { color: var(--accent-red); border-color: rgba(255, 82, 82, 0.4); }
.panes { flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); gap: 10px; }
.pane { min-height: 0; display: flex; flex-direction: column; background: var(--surface-1); border: 1px solid var(--line); border-radius: var(--radius-sm); overflow: hidden; }
.chat-scroll, .clue-scroll { flex: 1; min-height: 0; overflow-y: auto; }
.empty { text-align: center; font-size: 12px; color: var(--text-2); padding: 30px 0; }
.input-bar { display: flex; align-items: center; gap: 6px; padding: 8px; border-top: 1px solid var(--line); background: var(--surface-2); }
.chat-input { flex: 1; min-width: 0; background: var(--surface-0); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-0); font-size: 13px; padding: 6px 9px; }
.chat-input:disabled { opacity: 0.6; }
.chat-input::placeholder { color: var(--text-2); }
.toast { position: absolute; top: 12px; right: 14px; z-index: 40; background: var(--surface-2); border: 1px solid var(--accent-cyan-dim); border-radius: var(--radius-sm); padding: 8px 12px; font-size: 12px; color: var(--text-0); box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4); max-width: 60%; }
.toast.error { border-color: var(--accent-red); color: var(--accent-red); }
</style>
