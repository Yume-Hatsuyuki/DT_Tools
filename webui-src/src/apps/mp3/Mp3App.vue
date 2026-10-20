<script setup>
import { ref, reactive, computed, watch, onMounted, onUnmounted } from 'vue';
import { API, createPoller } from '../../api.js';
import FileBrowser from '../common/FileBrowser.vue';
import Mp3AddPanel from './Mp3AddPanel.vue';
import IconPlayerPlay from '~icons/tabler/player-play';
import IconPlayerPause from '~icons/tabler/player-pause';
import IconPlayerTrackPrev from '~icons/tabler/player-track-prev';
import IconPlayerTrackNext from '~icons/tabler/player-track-next';
import IconMicrophone from '~icons/tabler/microphone';
import IconMicrophoneOff from '~icons/tabler/microphone-off';
import IconVolume2 from '~icons/tabler/volume-2';
import IconVolume from '~icons/tabler/volume';
import IconRepeat from '~icons/tabler/repeat';
import IconRepeatOnce from '~icons/tabler/repeat-once';
import IconArrowsShuffle from '~icons/tabler/arrows-shuffle';
import IconFolderPlus from '~icons/tabler/folder-plus';
import IconTrash from '~icons/tabler/trash';
import IconPlaylistX from '~icons/tabler/playlist-x';
import IconArrowUp from '~icons/tabler/arrow-up';
import IconArrowDown from '~icons/tabler/arrow-down';
import IconGripVertical from '~icons/tabler/grip-vertical';
import IconPencil from '~icons/tabler/pencil';
import IconPlus from '~icons/tabler/plus';
import IconMusic from '~icons/tabler/music';

/**
 * 随身MP3：游戏机本机播放器（服务端播放，非浏览器播放）。选文件（WebUI 内置
 * 目录浏览）→ 歌单（多歌单，BepInEx/config/DT_Tools/mp3/ 持久化，全窗口/重启共享；
 * 每个歌单独立配置曲目，可拖动排序）→ 播放控制（上一首/播放暂停/下一首/停止 +
 * 可拖进度条 + 响度）。「混入麦克风」开关决定是否发给全房（语音未连接时仅本地出声），
 * 「本地出声」开关静音喇叭但进度照走（远程当 DJ 用）。与 /mic_music 共用同一播放会话。
 */
const phase = ref('idle');
const position = ref(0);
const duration = ref(0);
const volume = ref(0.85);
const mic = ref(true);          // 混入偏好（开关位置，持久化）
const micActive = ref(false);   // 当前会话是否真的在混入麦克风（语音未连接时偏好为开、此处为假）
const local = ref(true);
const mode = ref('list');
const lists = ref([]);          // 歌单库概览：[{ id, name, count }]
const activeId = ref('');       // 界面当前显示的歌单
const playingListId = ref('');  // 播放上下文所属歌单（空 = 无）
const playingLabel = ref('');   // 正在播放的曲目名（来自播放上下文，可能不在当前显示的歌单里）
const index = ref(-1);          // 正在播放的曲目在其歌单中的下标
const playlist = ref([]);       // 当前显示歌单的曲目
const loadError = ref('');
const toast = reactive({ text: '', error: false });
let toastTimer = null;
const busy = ref(false);        // 传输操作进行中（防重复点击）
const browsing = ref(false);
const addOpen = ref(false);   // 添加面板（本地 / https / 网易云）
const lastPath = ref(localStorage.getItem('dt_mp3_last') || '');

// 拖动进度/响度期间暂停轮询回写，松手提交
const seeking = ref(false);
const seekValue = ref(0);
const volumeDragging = ref(false);
const volumeValue = ref(0.85);

const poller = createPoller(reload, 1000);

async function reload() {
  const j = await API.mp3State();
  if (j.unauthorized) return;
  if (!j.ok) { loadError.value = j.error || '状态读取失败'; return; }
  loadError.value = '';
  phase.value = j.data?.phase || 'idle';
  duration.value = j.data?.duration || 0;
  if (!seeking.value) position.value = j.data?.position || 0;
  mic.value = !!j.data?.mic;
  micActive.value = !!j.data?.micActive;
  local.value = !!j.data?.local;
  mode.value = j.data?.mode || 'list';
  if (!volumeDragging.value) volume.value = j.data?.volume ?? 0.85;
  lists.value = j.data?.lists || [];
  activeId.value = j.data?.activeId || '';
  playingListId.value = j.data?.playingListId || '';
  playingLabel.value = j.data?.playingLabel || '';
  index.value = j.data?.index ?? -1;
  playlist.value = j.data?.playlist || [];
}

// 切歌/停止时强制结束拖动态：否则拖到结尾触发自动连播后，seeking 仍挂着
// 会把进度条和时间冻在旧曲目结尾（"UI 显示坏掉"的来源）
watch([index, duration, phase], () => {
  if (phase.value !== 'playing' || duration.value === 0) seeking.value = false;
});

function showToast(text, error = false) {
  toast.text = text;
  toast.error = error;
  clearTimeout(toastTimer);
  if (text) toastTimer = setTimeout(() => { toast.text = ''; }, 2600);
}

async function act(fn, okText) {
  if (busy.value) return;
  busy.value = true;
  try {
    const j = await fn();
    if (j.unauthorized) return;
    if (!j.ok) { showToast(j.data?.message || j.error || '操作失败', true); return false; }
    if (okText) showToast(j.data?.message || okText);
    await reload();
    return true;
  } finally {
    busy.value = false;
  }
}

const activeList = computed(() => lists.value.find(l => l.id === activeId.value) || null);
// 正在播放的那首属于当前显示的歌单时，才在列表里高亮
const playingHere = computed(() => !!playingListId.value && playingListId.value === activeId.value);
const highlightIndex = computed(() => (playingHere.value ? index.value : -1));

const playPause = () => {
  // 空闲时主按钮=「播放歌单」（显示 ▶，点击从当前歌单第一首开始）；
  // 播放中显示 ‖（点击暂停）、暂停中显示 ▶（点击恢复）——按钮永远显示点击后将做的事
  if (phase.value === 'idle') return act(() => API.mp3Index(0, activeId.value));
  if (phase.value === 'paused') return act(() => API.mp3Resume());
  return act(() => API.mp3Pause());
};
const prev = () => act(() => API.mp3Next(-1));
const next = () => act(() => API.mp3Next(1));

function onSeekCommit(e) {
  const v = Number(e.target.value);
  seeking.value = false;
  position.value = v;
  act(() => API.mp3Seek(v));
}
function onVolumeCommit(e) {
  const v = Number(e.target.value);
  volumeDragging.value = false;
  volume.value = v;
  act(() => API.mp3Volume(v));
}

// 开关标签：关=仅本地；开且会话在混入=混入麦克风；开但未混入（语音未连接/空闲）=混入待命
const micChip = computed(() => (!mic.value ? '仅本地' : micActive.value ? '混入麦克风' : '混入待命'));
const micTitle = computed(() => {
  if (!mic.value) return '纯本地播放（点击开启混入麦克风）';
  if (micActive.value) return '正在混入麦克风发往全房（点击关闭，变为纯本地播放）';
  return '已开启混入，当前未混入（语音未连接或未在播放）；本地出声不受影响（点击关闭）';
});
const micToggle = () => act(() => API.mp3Mic(!mic.value), true);
const localToggle = () => act(() => API.mp3Local(!local.value));

// 播放方式循环切换：列表循环 → 单曲循环 → 随机播放
const MODES = [
  { id: 'list', label: '列表循环', title: '列表循环：播完自动连播下一首（点击切换为单曲循环）' },
  { id: 'single', label: '单曲循环', title: '单曲循环：播完重播当前曲目（点击切换为随机播放）' },
  { id: 'random', label: '随机播放', title: '随机播放：播完随机连播、不重复当前（点击切换为列表循环）' },
];
const modeMeta = computed(() => MODES.find(m => m.id === mode.value) || MODES[0]);
const modeToggle = () => {
  const next = MODES[(MODES.findIndex(m => m.id === mode.value) + 1) % MODES.length];
  return act(() => API.mp3Mode(next.id));
};

const aliasEditing = ref(-1);
const aliasDraft = ref('');
function displayName(item) { return (item.alias || '').trim() || item.label; }
function startAlias(i, item) { aliasEditing.value = i; aliasDraft.value = item.alias || ''; }
function cancelAlias() { aliasEditing.value = -1; }
async function saveAlias(i) {
  if (aliasEditing.value !== i) return;   // 回车/Esc 已结束编辑，blur 不再重复提交
  const draft = aliasDraft.value;
  aliasEditing.value = -1;
  const r = await API.mp3PlaylistAlias(i, draft, activeId.value);
  if (!r.ok) showToast(r.data?.message || r.error || '保存失败', true);
  await reload();
}
async function onAdded(added) {
  addOpen.value = false;
  if (phase.value === 'idle') {
    const r = await API.mp3Index(added, activeId.value);
    if (!r.ok) showToast(r.data?.message || r.error || '播放失败', true);
  } else {
    showToast('已加入歌单');
  }
  await reload();
}

async function onPicked(path) {
  browsing.value = false;
  lastPath.value = path;
  try { localStorage.setItem('dt_mp3_last', path); } catch { /* 隐私模式忽略 */ }
  if (busy.value) return;
  busy.value = true;
  try {
    const j = await API.mp3PlaylistAdd(path, activeId.value);
    if (j.unauthorized) return;
    if (!j.ok) { showToast(j.data?.message || j.error || '添加失败', true); return; }
    const added = j.data?.index ?? -1;
    // 空闲时选完即播；正在播时只入队（歌单里点播放接管）。
    // 直接调 API 不走 act()——此时 busy 已占用，act 会静默吞掉这次播放
    if (phase.value === 'idle') {
      const r = await API.mp3Index(added, activeId.value);
      if (!r.ok) showToast(r.data?.message || r.error || '播放失败', true);
    } else {
      showToast('已加入歌单');
    }
    await reload();
  } finally {
    busy.value = false;
  }
}

// 点正在播的行 = 切换暂停/恢复（像常规播放器）；点其他行 = 切歌。
function playAt(i) {
  if (i === highlightIndex.value && playingSomewhere.value) {
    return phase.value === 'paused' ? act(() => API.mp3Resume()) : act(() => API.mp3Pause());
  }
  return act(() => API.mp3Index(i, activeId.value));
}
const moveUp = (i) => act(() => API.mp3PlaylistMove(i, i - 1, activeId.value));
const moveDown = (i) => act(() => API.mp3PlaylistMove(i, i + 1, activeId.value));
const removeAt = (i) => act(() => API.mp3PlaylistRemove(i, activeId.value), '已移除');
const clearAll = () => {
  if (!playlist.value.length) return;
  if (!confirm(`清空歌单「${activeList.value?.name || ''}」全部 ${playlist.value.length} 首？（当前播放不受影响）`)) return;
  act(() => API.mp3PlaylistClear(activeId.value));
};

// ---- 多歌单：切换 / 新建 / 重命名 / 删除 ----
const creating = ref(false);
const draftName = ref('');
const renamingId = ref('');
const renameText = ref('');

// 输入框出现时自动聚焦并全选（重命名失败回填时同样生效）
const vFocus = { mounted: (el) => { el.focus(); el.select?.(); } };

function selectList(id) {
  if (id === activeId.value) return;
  act(() => API.mp3ListSelect(id));
}

function startCreate() {
  renamingId.value = '';
  draftName.value = '';
  creating.value = true;
}

async function commitCreate() {
  // 回车与失焦会各触发一次：先关闭输入框，保证只提交一次
  if (!creating.value) return;
  const name = draftName.value.trim();
  creating.value = false;
  if (!name) return;
  const ok = await act(() => API.mp3ListCreate(name), '已新建歌单');
  if (ok === false) { creating.value = true; draftName.value = name; }
}

function startRename(list) {
  creating.value = false;
  renamingId.value = list.id;
  renameText.value = list.name;
}

async function commitRename() {
  if (!renamingId.value) return;
  const id = renamingId.value;
  const name = renameText.value.trim();
  const old = lists.value.find(l => l.id === id)?.name || '';
  renamingId.value = '';
  if (!name || name === old) return;
  const ok = await act(() => API.mp3ListRename(id, name), '已重命名');
  if (ok === false) { renamingId.value = id; renameText.value = name; }
}

function cancelRename() {
  renamingId.value = '';
}

async function deleteList(list) {
  if (lists.value.length <= 1) { showToast('至少保留一个歌单', true); return; }
  if (!confirm(`删除歌单「${list.name}」（${list.count} 首）？`)) return;
  await act(() => API.mp3ListDelete(list.id), '歌单已删除');
}

// 歌单按文件名过滤：只影响显示，不改歌单顺序。每行保留歌单原下标，
// 播放 / 移除 / 上下移 / 拖动都用原下标，所以过滤后操作的仍是真实的那一首。
const nameFilter = ref('');
const filterKeyword = computed(() => nameFilter.value.trim());
const filtering = computed(() => filterKeyword.value !== '');
const visibleRows = computed(() => {
  const rows = playlist.value.map((item, i) => ({ item, i }));
  if (!filtering.value) return rows;
  const q = filterKeyword.value.toLowerCase();
  return rows.filter(({ item }) => displayName(item).toLowerCase().includes(q));
});
const listHeadText = computed(() => {
  const name = activeList.value?.name || '歌单';
  return filtering.value
    ? `${name}（匹配 ${visibleRows.value.length} / ${playlist.value.length}）`
    : `${name}（${playlist.value.length}）`;
});

// ---- 拖动排序（pointer 事件：鼠标与触摸通用）----
// 拖动期间只记录目标位置，松手才提交一次 move，避免中途多次写盘
const listEl = ref(null);
const drag = reactive({ active: false, from: -1, slot: -1, pointerId: null });

// 插入位置 slot 是「插到第 slot 行之前」；移除原行后的最终下标需要修正
function finalDropIndex(from, slot) {
  return slot > from ? slot - 1 : slot;
}
const dropPreview = computed(() => drag.active && finalDropIndex(drag.from, drag.slot) !== drag.from);

function slotAt(clientY) {
  const rows = listEl.value ? listEl.value.querySelectorAll('.row[data-idx]') : [];
  for (const el of rows) {
    const r = el.getBoundingClientRect();
    if (clientY < r.top + r.height / 2) return Number(el.dataset.idx);
  }
  return playlist.value.length;
}

function onGripDown(e, i) {
  if (busy.value || filtering.value || playlist.value.length < 2) return;
  e.preventDefault();
  drag.active = true;
  drag.from = i;
  drag.slot = i;
  drag.pointerId = e.pointerId ?? null;
  window.addEventListener('pointermove', onDragMove);
  window.addEventListener('pointerup', onDragEnd);
  window.addEventListener('pointercancel', onDragCancel);
}

function onDragMove(e) {
  if (!drag.active || (e.pointerId ?? null) !== drag.pointerId) return;
  drag.slot = slotAt(e.clientY);
}

function stopDrag() {
  window.removeEventListener('pointermove', onDragMove);
  window.removeEventListener('pointerup', onDragEnd);
  window.removeEventListener('pointercancel', onDragCancel);
}

function resetDrag() {
  drag.active = false;
  drag.from = -1;
  drag.slot = -1;
  drag.pointerId = null;
  stopDrag();
}

function onDragEnd(e) {
  if (!drag.active || (e.pointerId ?? null) !== drag.pointerId) return;
  const from = drag.from;
  const to = finalDropIndex(drag.from, drag.slot);
  resetDrag();
  if (to !== from && to >= 0) act(() => API.mp3PlaylistMove(from, to, activeId.value));
}

function onDragCancel(e) {
  if (!drag.active || (e.pointerId ?? null) !== drag.pointerId) return;
  resetDrag();
}

const phaseText = computed(() => ({
  idle: '未播放', loading: '加载中', playing: '播放中', paused: '已暂停',
}[phase.value] || phase.value));
const playingSomewhere = computed(() => phase.value === 'playing' || phase.value === 'paused');
const currentLabel = computed(() => {
  if (playingSomewhere.value) return playingLabel.value || '（其他入口正在使用播放器）';
  return '未选择曲目';
});

function fmt(t) {
  const v = Number(t);
  if (!isFinite(v) || v <= 0) return '0:00';
  const m = Math.floor(v / 60);
  const s = Math.floor(v % 60);
  return `${m}:${String(s).padStart(2, '0')}`;
}

onMounted(() => {
  reload();
  poller.start();
});
onUnmounted(() => {
  poller.stop();
  stopDrag();
});
</script>

<template>
  <div class="mp3-app">
    <div class="toolbar">
      <div class="toolbar-left">
        <span class="chip" :class="{ on: phase === 'playing' }">{{ phaseText }}</span>
        <span class="chip" :class="{ on: mic }">{{ micChip }}</span>
      </div>
      <div class="toolbar-right">
        <button class="tb-btn" :disabled="busy" :title="modeMeta.title" @click="modeToggle">
          <IconRepeat v-if="mode === 'list'" />
          <IconRepeatOnce v-else-if="mode === 'single'" />
          <IconArrowsShuffle v-else />
          {{ modeMeta.label }}
        </button>
        <button class="tb-btn" :disabled="busy" title="添加本地文件、https 链接或网易云音乐" @click="addOpen = true">
          <IconFolderPlus /> 添加歌曲
        </button>
        <button class="tb-btn" :disabled="busy || !playlist.length" title="清空当前歌单（不影响当前播放）" @click="clearAll">
          <IconPlaylistX /> 清空
        </button>
      </div>
    </div>

    <div class="player">
      <div class="now-playing">
        <IconMusic class="cover" />
        <div class="meta">
          <div class="title" :title="currentLabel">{{ currentLabel }}</div>
          <div class="time">{{ fmt(position) }} / {{ fmt(duration) }}</div>
        </div>
      </div>

      <input
        class="seek"
        type="range"
        min="0"
        :max="duration || 0"
        step="0.1"
        :value="seeking ? seekValue : position"
        :disabled="!duration"
        @pointerdown="seeking = true; seekValue = position"
        @input="seekValue = Number($event.target.value)"
        @change="onSeekCommit"
      >

      <div class="transport">
        <button class="icon-btn" :disabled="busy || !playlist.length" title="上一首（歌单循环）" @click="prev">
          <IconPlayerTrackPrev />
        </button>
        <button
          class="icon-btn main"
          :disabled="busy || (!playingSomewhere && !playlist.length)"
          :title="phase === 'playing' || phase === 'loading' ? '暂停' : (phase === 'paused' ? '继续播放' : '播放当前歌单第一首')"
          @click="playPause"
        >
          <IconPlayerPause v-if="phase === 'playing' || phase === 'loading'" />
          <IconPlayerPlay v-else />
        </button>
        <button class="icon-btn" :disabled="busy || !playlist.length" title="下一首（歌单循环）" @click="next">
          <IconPlayerTrackNext />
        </button>

        <button
          class="icon-btn"
          :class="{ on: mic }"
          :disabled="busy"
          :title="micTitle"
          @click="micToggle"
        >
          <IconMicrophone v-if="mic" />
          <IconMicrophoneOff v-else />
        </button>
        <button
          class="icon-btn"
          :class="{ on: local }"
          :disabled="busy"
          :title="local ? '本地喇叭出声中（点击静音，进度照走）' : '本地喇叭已静音（点击恢复）'"
          @click="localToggle"
        >
          <IconVolume2 v-if="local" />
          <IconVolume v-else />
        </button>

        <div class="volume">
          <input
            class="volume-range"
            type="range"
            min="0"
            max="1"
            step="0.05"
            :value="volumeDragging ? volumeValue : volume"
            @pointerdown="volumeDragging = true; volumeValue = volume"
            @input="volumeValue = Number($event.target.value)"
            @change="onVolumeCommit"
          >
          <span class="volume-num">{{ Math.round((volumeDragging ? volumeValue : volume) * 100) }}%</span>
        </div>
      </div>
    </div>

    <div class="lists-bar" role="tablist" aria-label="歌单">
      <template v-for="l in lists" :key="l.id">
        <input
          v-if="renamingId === l.id"
          v-model="renameText"
          v-focus
          class="list-rename"
          maxlength="40"
          spellcheck="false"
          placeholder="歌单名"
          @keydown.enter.prevent="commitRename"
          @keydown.esc.prevent="cancelRename"
          @blur="commitRename"
        >
        <button
          v-else
          class="list-tab"
          :class="{ active: l.id === activeId }"
          role="tab"
          :aria-selected="l.id === activeId"
          :title="l.id === activeId ? '双击或点铅笔重命名' : '切换到这个歌单'"
          @click="selectList(l.id)"
          @dblclick="startRename(l)"
        >
          <span>{{ l.name }}</span>
          <span class="cnt">{{ l.count }}</span>
          <span v-if="l.id === playingListId" class="playing" title="正在播放">▶</span>
        </button>
      </template>
      <input
        v-if="creating"
        v-model="draftName"
        v-focus
        class="list-rename"
        maxlength="40"
        spellcheck="false"
        placeholder="新歌单名"
        @keydown.enter.prevent="commitCreate"
        @keydown.esc.prevent="creating = false"
        @blur="commitCreate"
      >
      <button v-else class="list-tab icon" title="新建歌单" :disabled="busy" @click="startCreate">
        <IconPlus />
      </button>
      <button
        v-if="activeList && !renamingId"
        class="list-tab icon"
        title="重命名当前歌单"
        :disabled="busy"
        @click="startRename(activeList)"
      >
        <IconPencil />
      </button>
      <button
        v-if="activeList && lists.length > 1 && !renamingId"
        class="list-tab icon danger"
        title="删除当前歌单"
        :disabled="busy"
        @click="deleteList(activeList)"
      >
        <IconTrash />
      </button>
    </div>

    <div class="list-head">
      <span>{{ listHeadText }}</span>
      <input v-model="nameFilter" class="filter" type="search" spellcheck="false" placeholder="按文件名过滤…">
    </div>
    <div ref="listEl" class="list">
      <div v-if="!playlist.length" class="empty">这个歌单还没有歌曲——点「添加歌曲」从游戏所在机器选音频文件（mp3/ogg/wav…）。</div>
      <div v-else-if="!visibleRows.length" class="empty">没有文件名包含「{{ filterKeyword }}」的歌曲。</div>
      <div
        v-for="{ item, i } in visibleRows"
        :key="item.path"
        :data-idx="i"
        class="row"
        :class="{
          current: i === highlightIndex && playingSomewhere,
          dragging: drag.active && drag.from === i,
          'drop-before': dropPreview && drag.slot === i,
          'drop-after': dropPreview && drag.slot === playlist.length && i === playlist.length - 1,
        }"
      >
        <span
          class="grip"
          :class="{ disabled: busy || filtering || playlist.length < 2 }"
          :title="filtering ? '清空过滤后再拖动排序' : '按住拖动排序'"
          @pointerdown="onGripDown($event, i)"
        >
          <IconGripVertical />
        </span>
        <span class="no">{{ i + 1 }}</span>
        <template v-if="aliasEditing === i">
              <input class="alias-input" v-model="aliasDraft" maxlength="60" placeholder="标签（留空恢复原名）"
                @keydown.enter.prevent="saveAlias(i)" @keydown.esc="cancelAlias" @blur="saveAlias(i)" />
            </template>
            <template v-else>
              <span class="label" :title="item.path">{{ displayName(item) }}</span>
              <button class="icon-btn alias-btn" title="编辑标签（不改文件名）" @click.stop="startAlias(i, item)"><IconPencil /></button>
            </template>
        <button
          class="mini-btn"
          :class="{ active: i === highlightIndex && playingSomewhere }"
          :disabled="busy"
          :title="i === highlightIndex && phase === 'playing' ? '暂停（当前曲目）' : (i === highlightIndex && phase === 'paused' ? '继续播放（当前曲目）' : '播放这首')"
          @click="playAt(i)"
        >
          <IconPlayerPause v-if="i === highlightIndex && phase === 'playing'" />
          <IconPlayerPlay v-else />
        </button>
        <button class="mini-btn" :disabled="busy || filtering || i === 0" :title="filtering ? '清空过滤后再调整顺序' : '上移'" @click="moveUp(i)"><IconArrowUp /></button>
        <button class="mini-btn" :disabled="busy || filtering || i === playlist.length - 1" :title="filtering ? '清空过滤后再调整顺序' : '下移'" @click="moveDown(i)"><IconArrowDown /></button>
        <button class="mini-btn remove" :disabled="busy" title="从歌单移除" @click="removeAt(i)"><IconTrash /></button>
      </div>
    </div>

    <div v-if="toast.text" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <Mp3AddPanel v-if="addOpen" :list-id="activeId" @close="addOpen = false" @pick-local="addOpen = false; browsing = true" @added="onAdded" @error="(m) => showToast(m, true)" />
    <FileBrowser
      v-if="browsing"
      :start-path="lastPath"
      title="选择音频文件"
      @select="onPicked"
      @close="browsing = false"
    />

    <div class="foot">
      <span v-if="loadError" class="foot-err">{{ loadError }}</span>
      <template v-else>
        <span>播放走游戏所在机器：混入麦克风发给全房（需游戏内麦克风未静音、非按键说话），或仅本地出声；语音未连接时自动降级为仅本地。暂停即销毁会话（语音管线还原），播放时从原进度重建；播完按播放方式在所属歌单内连播。</span>
      </template>
    </div>
  </div>
</template>

<style scoped>
.mp3-app { display: flex; flex-direction: column; height: 100%; background: var(--surface-0); position: relative; }

.toolbar {
  display: flex; align-items: center; justify-content: space-between; gap: 10px;
  padding: 10px 14px; flex-shrink: 0; flex-wrap: wrap;
  background: var(--surface-1); border-bottom: 1px solid var(--line);
}
.toolbar-left { display: flex; align-items: center; gap: 6px; }
.toolbar-right { display: flex; align-items: center; gap: 6px; }

.chip {
  font-size: 11px; color: var(--text-2);
  border: 1px solid var(--line); border-radius: 999px; padding: 2px 9px; white-space: nowrap;
}
.chip.on { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }

.tb-btn {
  display: flex; align-items: center; gap: 5px;
  background: var(--surface-2); border: 1px solid var(--line); border-radius: var(--radius-sm);
  color: var(--text-1); font-size: 11.5px; padding: 6px 10px; cursor: pointer; white-space: nowrap;
}
.tb-btn svg { width: 13px; height: 13px; }
.tb-btn:hover:not(:disabled) { color: var(--text-0); border-color: var(--line-strong); }
.tb-btn:disabled { opacity: 0.45; cursor: default; }

.player { padding: 14px 16px 10px; flex-shrink: 0; border-bottom: 1px solid var(--line); background: var(--surface-1); }

.now-playing { display: flex; align-items: center; gap: 12px; margin-bottom: 10px; }
.cover { width: 40px; height: 40px; color: var(--shell-glow); flex-shrink: 0; opacity: 0.9; }
.meta { min-width: 0; }
.title {
  font-size: 13.5px; font-weight: 600; color: var(--text-0);
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 100%;
}
.time { font-family: var(--font-mono); font-size: 11px; color: var(--text-2); margin-top: 3px; }

input[type="range"] { -webkit-appearance: none; appearance: none; width: 100%; height: 4px; border-radius: 2px; background: var(--line-strong); outline: none; cursor: pointer; }
input[type="range"]:disabled { opacity: 0.4; cursor: default; }
input[type="range"]::-webkit-slider-thumb {
  -webkit-appearance: none; appearance: none; width: 12px; height: 12px; border-radius: 50%;
  background: var(--accent-cyan); border: none; box-shadow: 0 0 6px rgba(0, 229, 255, 0.5);
}
input[type="range"]::-moz-range-thumb { width: 12px; height: 12px; border-radius: 50%; background: var(--accent-cyan); border: none; }
.seek { margin-bottom: 12px; }

.transport { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }

.icon-btn {
  width: 34px; height: 34px; display: grid; place-items: center;
  background: var(--surface-2); border: 1px solid var(--line); border-radius: var(--radius-sm);
  color: var(--text-1); cursor: pointer; flex-shrink: 0;
}
.icon-btn svg { width: 16px; height: 16px; }
.icon-btn:hover:not(:disabled) { color: var(--text-0); border-color: var(--line-strong); }
.icon-btn:disabled { opacity: 0.45; cursor: default; }
.icon-btn.main { width: 42px; height: 42px; background: var(--accent-blue-btn, var(--surface-2)); color: #fff; }
.icon-btn.danger:hover:not(:disabled) { color: var(--accent-red); border-color: var(--accent-red); }
.icon-btn.on { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }

.volume { display: flex; align-items: center; gap: 8px; flex: 1; min-width: 130px; margin-left: auto; }
.volume-range { max-width: 140px; }
.volume-num { font-family: var(--font-mono); font-size: 11px; color: var(--text-2); width: 36px; text-align: right; }

.lists-bar {
  flex-shrink: 0; display: flex; align-items: center; gap: 6px;
  padding: 8px 14px 0; overflow-x: auto; scrollbar-width: thin;
}
.list-tab {
  display: inline-flex; align-items: center; gap: 5px; flex-shrink: 0;
  background: var(--surface-1); border: 1px solid var(--line); border-radius: 999px;
  color: var(--text-2); font-size: 12px; padding: 4px 10px; cursor: pointer; white-space: nowrap;
}
.list-tab svg { width: 13px; height: 13px; }
.list-tab:hover:not(:disabled) { color: var(--text-0); border-color: var(--line-strong); }
.list-tab.active { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); background: var(--surface-2); }
.list-tab .cnt { font-family: var(--font-mono); font-size: 10.5px; opacity: 0.8; }
.list-tab .playing { font-size: 9px; color: var(--accent-cyan); }
.list-tab.icon { width: 26px; height: 26px; padding: 0; justify-content: center; }
.list-tab.icon.danger:hover:not(:disabled) { color: var(--accent-red); border-color: var(--accent-red); }
.list-tab:disabled { opacity: 0.45; cursor: default; }
.list-rename {
  flex-shrink: 0; width: 140px; background: var(--surface-0); border: 1px solid var(--accent-cyan-dim);
  border-radius: 999px; color: var(--text-0); font-size: 12px; padding: 4px 10px; outline: none;
}

.list-head {
  flex-shrink: 0; padding: 8px 14px 4px;
  display: flex; align-items: center; justify-content: space-between; gap: 10px;
  font-size: 11px; color: var(--text-2); text-transform: none;
}
.filter {
  width: 100%; max-width: 200px; min-width: 0;
  background: var(--surface-0); border: 1px solid var(--line); border-radius: var(--radius-sm);
  color: var(--text-0); font-size: 12px; padding: 4px 8px; outline: none;
}
.list { flex: 1; overflow-y: auto; padding: 4px 14px 12px; display: flex; flex-direction: column; gap: 6px; }

.empty { border: 1px dashed var(--line-strong); border-radius: var(--radius-sm); color: var(--text-2); font-size: 12px; padding: 14px 12px; text-align: center; }

.row {
  display: flex; align-items: center; gap: 6px; user-select: none;
  background: var(--surface-1); border: 1px solid var(--line); border-radius: var(--radius-sm);
  padding: 6px 8px;
}
.row.current { border-color: var(--accent-cyan-dim); }
.row.dragging { opacity: 0.45; }
.row.drop-before { box-shadow: 0 -2px 0 0 var(--accent-cyan); }
.row.drop-after { box-shadow: 0 2px 0 0 var(--accent-cyan); }
.grip {
  display: grid; place-items: center; width: 18px; height: 26px; flex-shrink: 0;
  color: var(--text-2); cursor: grab; touch-action: none;
}
.grip svg { width: 14px; height: 14px; }
.grip.disabled { opacity: 0.3; cursor: default; }
.no { font-family: var(--font-mono); font-size: 11px; color: var(--text-2); width: 22px; text-align: right; flex-shrink: 0; }
.label { flex: 1; min-width: 0; font-size: 12.5px; color: var(--text-1); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.row.current .label { color: var(--accent-cyan); }

.mini-btn {
  width: 26px; height: 26px; display: grid; place-items: center; flex-shrink: 0;
  background: transparent; border: 1px solid transparent; border-radius: var(--radius-sm);
  color: var(--text-2); cursor: pointer;
}
.mini-btn svg { width: 13px; height: 13px; }
.mini-btn.active { color: var(--accent-cyan); }
.mini-btn:hover:not(:disabled) { color: var(--text-0); border-color: var(--line-strong); background: var(--surface-2); }
.mini-btn:disabled { opacity: 0.35; cursor: default; }
.mini-btn.remove:hover:not(:disabled) { color: var(--accent-red); border-color: var(--accent-red); }

.toast {
  position: absolute; top: 56px; right: 16px;
  background: var(--surface-2); border: 1px solid var(--accent-cyan-dim); border-radius: var(--radius-sm);
  padding: 8px 14px; font-size: 12px; color: var(--text-0); z-index: 100;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4); max-width: 70%;
}
.toast.error { border-color: var(--accent-red); color: var(--accent-red); }

.foot {
  flex-shrink: 0; border-top: 1px solid var(--line); background: var(--surface-1);
  padding: 7px 14px; font-size: 11px; color: var(--text-2);
  display: flex; align-items: center; gap: 6px;
}
.foot-err { color: var(--accent-red); }
.alias-btn { opacity: 0.6; }
.alias-btn:hover { opacity: 1; }
.alias-input { flex: 1; min-width: 0; padding: 2px 6px; border-radius: 4px; border: 1px solid rgba(255,255,255,0.2); background: rgba(0,0,0,0.25); color: inherit; font-size: 12.5px; }
</style>
