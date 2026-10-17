<script setup>
import { ref, computed, nextTick, onMounted, onUnmounted } from 'vue';
import { API, createPoller } from '../../api.js';
import { pickImageFile } from '../../composables/pickImage.js';
import TabletChat from './TabletChat.vue';
import TabletClues from './TabletClues.vue';
import TabletExportDialog from './TabletExportDialog.vue';
import { orderClues, formatChatText, formatClueText, fileStampOf } from './tabletFormat.js';
import { renderChatParts, renderClueParts, downloadBlob, downloadText, fileToJpegBase64 } from './tabletImage.js';

/**
 * 平板电脑（实验性）：左侧聊天、右侧线索。数据由后端在游戏内采集，前端每 0.8 秒增量拉取。
 * 发言只在大厅与学级裁判可用；照片只在学级裁判的讨论阶段可用（均由后端与游戏双重校验）。
 */

const POLL_MS = 800;
const PHOTO_LIMIT = 163840;
const LINE_CAP = 1000;

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
let stickBottom = true;

const input = ref('');
const sending = ref(false);
const photoBusy = ref(false);
const menu = ref('');
const exportDialog = ref(null);
const confirmClear = ref(false);

const clueList = computed(() => orderClues(clues.value));
const canSendPhoto = computed(() => stateCode.value === 'Trial' && canType.value);
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

async function pickPhoto() {
  if (!canSendPhoto.value || photoBusy.value) return;
  const file = await pickImageFile('image/*');
  if (!file) return;
  photoBusy.value = true;
  try {
    const data = await fileToJpegBase64(file, PHOTO_LIMIT);
    const j = await API.tabletPhoto(data);
    if (j.unauthorized) return;
    if (!j.ok) {
      flash(j.data?.message || j.error || '照片发送失败', true);
      return;
    }
    flash(j.data?.message || '照片已发送');
    stickBottom = true;
    poll();
  } catch (e) {
    flash(e.message || '照片处理失败', true);
  } finally {
    photoBusy.value = false;
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
      const text = kind === 'chat' ? formatChatText(selected) : formatClueText(selected);
      downloadText(text, `${title}_${stamp}.txt`);
      flash(`已导出 ${selected.length} 条`);
      return;
    }
    flash('正在生成图片…');
    const blobs = kind === 'chat'
      ? await renderChatParts(selected, title)
      : await renderClueParts(selected, title);
    blobs.forEach((blob, i) => {
      const part = blobs.length > 1 ? `_${i + 1}` : '';
      downloadBlob(blob, `${title}_${stamp}${part}.png`);
    });
    flash(blobs.length > 1 ? `已导出 ${blobs.length} 张图片` : '已导出图片');
  } catch (e) {
    flash(`导出失败：${e.message || e}`, true);
  }
}

async function doClear() {
  confirmClear.value = false;
  const j = await API.tabletClear();
  if (j.unauthorized) return;
  if (!j.ok) {
    flash(j.error || '清理失败', true);
    return;
  }
  lines.value = [];
  clues.value = [];
  cursor.value = 0;
  flash('已清理');
  poll();
}

onMounted(() => poller.start());
onUnmounted(() => poller.stop());
</script>

<template>
  <div class="tablet-app">
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
        <button class="tb-btn danger" @click="confirmClear = true">清理</button>
      </div>
    </header>

    <div v-if="confirmClear" class="confirm">
      <span>清空本地记录（聊天、线索、照片）？游戏内容不受影响。</span>
      <button class="tb-btn" @click="confirmClear = false">取消</button>
      <button class="tb-btn danger" @click="doClear">确认清理</button>
    </div>

    <div v-if="enabled === false" class="notice">
      「平板电脑（实验性）」未启用。请在 DT 配置中打开该功能后再使用。
    </div>
    <div v-else-if="error" class="notice err">读取失败：{{ error }}</div>

    <div class="panes">
      <section class="pane chat-pane">
        <div ref="chatEl" class="chat-scroll" @scroll="onChatScroll">
          <TabletChat :lines="lines" />
          <div v-if="!lines.length" class="empty">暂无聊天记录</div>
        </div>
        <div class="input-bar">
          <button
            class="tb-btn"
            :disabled="!canSendPhoto || photoBusy"
            :title="canSendPhoto ? '发送照片' : '仅学级裁判的讨论阶段可以发送照片'"
            @click="pickPhoto"
          >{{ photoBusy ? '处理中…' : '图片' }}</button>
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
          <TabletClues :clues="clueList" />
        </div>
      </section>
    </div>

    <div v-if="toast.text" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <TabletExportDialog
      v-if="exportDialog"
      :kind="exportDialog.kind"
      :format="exportDialog.format"
      :items="exportItems"
      @cancel="exportDialog = null"
      @confirm="runExport"
    />
  </div>
</template>

<style scoped>
.tablet-app { position: relative; display: flex; flex-direction: column; gap: 8px; height: 100%; box-sizing: border-box; padding: 10px 12px; background: var(--surface-0); color: var(--text-1); overflow: hidden; }
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
.menu button { text-align: left; background: none; border: none; color: var(--text-1); font-size: 12px; padding: 7px 12px; cursor: pointer; }
.menu button:hover { background: rgba(140, 180, 255, 0.08); color: var(--text-0); }
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
