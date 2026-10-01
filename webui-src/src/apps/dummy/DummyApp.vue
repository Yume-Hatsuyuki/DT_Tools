<script setup>
import { ref, reactive, computed, watch, onMounted, onUnmounted } from 'vue';
import { API, createPoller } from '../../api.js';
import IconPlus from '~icons/tabler/plus';
import IconX from '~icons/tabler/x';
import IconRefresh from '~icons/tabler/refresh';
import IconEraser from '~icons/tabler/eraser';
import IconChecks from '~icons/tabler/checks';
import IconUserCheck from '~icons/tabler/user-check';
import IconUserMinus from '~icons/tabler/user-minus';
import IconListCheck from '~icons/tabler/list-check';

/**
 * 假人管理：多假人配置列表（昵称 + 角色下拉）→「准备」进场并就绪 →「选角」。
 * 行配置持久在 localStorage；假人本体走游戏原生 Dummy 语义——
 * 回大厅时游戏会自动清场（行配置保留，再点「准备」即可重新进场）。
 */
const ROWS_KEY = 'dt_dummy_rows_v1';

const rows = ref(loadRows());
const characters = ref([]);            // [{id, label}]
const gameState = ref('NoneState');
const isHost = ref(false);
const capacity = ref(8);
const livePlayers = ref([]);           // 房间快照中的全部玩家
const loadError = ref('');
const toast = reactive({ text: '', error: false });
let toastTimer = null;
const busyKey = ref('');               // 正在执行操作的行（防重复点击）

let rowSeq = Date.now() % 100000;
function nextRowId() { return `r${++rowSeq}`; }

function loadRows() {
  try {
    const parsed = JSON.parse(localStorage.getItem(ROWS_KEY));
    if (Array.isArray(parsed)) {
      return parsed.filter(r => r && typeof r === 'object').map(r => ({
        id: r.id || nextRowId(),
        name: typeof r.name === 'string' ? r.name : '',
        characterId: Number.isFinite(r.characterId) ? r.characterId : -2,
      }));
    }
  } catch { /* 损坏即重建 */ }
  return [];
}

watch(rows, (val) => {
  try { localStorage.setItem(ROWS_KEY, JSON.stringify(val)); } catch { /* 隐私模式忽略 */ }
}, { deep: true });

function showToast(text, error = false) {
  toast.text = text;
  toast.error = error;
  clearTimeout(toastTimer);
  if (text) toastTimer = setTimeout(() => { toast.text = ''; }, 2600);
}

// ---- 数据加载 ----

const poller = createPoller(reload, 1000);

async function reload() {
  const j = await API.dummyState();
  if (j.unauthorized) return;
  if (!j.ok) { loadError.value = j.error || '状态读取失败'; return; }
  loadError.value = '';
  gameState.value = j.data?.gameState || 'NoneState';
  isHost.value = !!j.data?.isHost;
  capacity.value = j.data?.capacity || 8;
  livePlayers.value = j.data?.players || [];
  // 角色目录未就绪时随轮询补拉（数据表在游戏加载完成后才可用）
  if (!characters.value.length) await loadCharacters();
}

async function loadCharacters() {
  const j = await API.dummyCharacters();
  if (j.unauthorized) return;
  if (j.ok && Array.isArray(j.data?.characters)) characters.value = j.data.characters;
}

// ---- 行状态派生 ----

function liveOf(row) {
  const name = row.name.trim();
  return livePlayers.value.find(p => p.ours && name && p.name === name) || null;
}

const liveCount = computed(() => livePlayers.value.filter(p => p.ours).length);
const canOperate = computed(() => isHost.value && gameState.value === 'Lobby');
const canPick = computed(() => isHost.value && (gameState.value === 'Lobby' || gameState.value === 'PickCharacter'));
const inPickPhase = computed(() => gameState.value === 'PickCharacter');
/**
 * 假人可用席位 = 房间人数上限 − 非本应用创建的玩家数。
 * 真人（含房主）与游戏原生假人（真实玩家掉线的残留体）都占席位，动态扣减；
 * 未进房时按房主 1 席兜底。与后端判定等价：Players.Count（全员）< 上限才可再创建。
 */
const occupiedByOthers = computed(() => Math.max(1, livePlayers.value.filter(p => !p.ours).length));
const usableCapacity = computed(() => Math.max(0, capacity.value - occupiedByOthers.value));

const stateLabels = {
  NoneState: '未进房',
  Lobby: '大厅',
  PickCharacter: '选角',
  Survive: '生存',
  Detective: '侦探',
  Trial: '审判',
  TotalResult: '结算',
};
const stateText = computed(() => stateLabels[gameState.value] || gameState.value);

function charLabel(id) {
  const c = characters.value.find(c => c.id === id);
  return c ? c.label : (id === -2 ? '随机' : `#${id}`);
}

function defaultName() {
  const used = new Set(rows.value.map(r => r.name.trim()));
  for (let i = 1; i <= 99; i++) {
    if (!used.has(`假人${i}`)) return `假人${i}`;
  }
  return `假人${rows.value.length + 1}`;
}

function addRow() {
  if (rows.value.length >= usableCapacity.value) {
    showToast(`假人席位已满：房间上限 ${capacity.value} 人，真人等已占 ${occupiedByOthers.value} 席`, true);
    return;
  }
  rows.value.push({ id: nextRowId(), name: defaultName(), characterId: -2 });
}

function removeRow(row) {
  const live = liveOf(row);
  if (live) {
    if (!confirm(`移出假人「${row.name.trim() || live.name}」并删除该行？`)) return;
    ejectRow(row, live);
    return;
  }
  rows.value = rows.value.filter(r => r.id !== row.id);
}

async function ejectRow(row, live) {
  busyKey.value = row.id;
  const j = await API.dummyRemove(live.name);
  busyKey.value = '';
  if (j.unauthorized) return;
  rows.value = rows.value.filter(r => r.id !== row.id);
  if (!j.ok) { showToast(j.data?.message || j.error || '移出失败', true); return; }
  showToast(j.data?.message || '已移出');
}

// ---- 行操作 ----

/** 准备：未进场 → 创建假人 + 自动就绪；已进场 → 切换就绪态。 */
async function prepare(row) {
  const name = row.name.trim();
  if (!name) { showToast('昵称不能为空', true); return; }
  busyKey.value = row.id;
  try {
    const live = liveOf(row);
    if (live) {
      const j = await API.dummyReady(live.name, !live.ready);
      handleResult(j, row.id);
      return;
    }
    const j = await API.dummyCreate(name, row.characterId);
    if (j.unauthorized) return;
    if (!j.ok) { showToast(j.data?.message || j.error || '创建失败', true); return; }
    // 创建成功即就绪（大厅开局要求除房主外全员准备）
    const rj = await API.dummyReady(name, true);
    handleResult(rj, true);
  } finally {
    busyKey.value = '';
  }
}

/** 选角：大厅换角 / 选角阶段锁定角色。 */
async function pick(row) {
  const live = liveOf(row);
  if (!live) { showToast('该行还没有进场的假人', true); return; }
  busyKey.value = row.id;
  try {
    const j = await API.dummyPick(live.name, row.characterId);
    handleResult(j);
  } finally {
    busyKey.value = '';
  }
}

function handleResult(j, created = false) {
  if (j.unauthorized) return;
  if (!j.ok) { showToast(j.data?.message || j.error || '操作失败', true); return; }
  showToast(created ? `${j.data?.message || '已进场'}（已准备）` : (j.data?.message || '完成'));
}

async function removeAll() {
  const live = livePlayers.value.filter(p => p.ours);
  if (!live.length) { showToast('房间里没有假人'); return; }
  if (!confirm(`移出全部 ${live.length} 个假人？（行配置保留）`)) return;
  busyKey.value = '@all';
  try {
    const j = await API.dummyRemoveAll();
    if (j.unauthorized) return;
    if (!j.ok) { showToast(j.data?.message || j.error || '移出失败', true); return; }
    showToast(j.data?.message || '已移出');
  } finally {
    busyKey.value = '';
  }
}

/**
 * 一键准备：遍历全部行——未进场 = 创建+自动就绪；已进场未准备 = 就绪；
 * 已准备/昵称为空 = 跳过。中途失败不中断，汇总 toast。
 */
async function prepareAll() {
  if (busyKey.value) return;
  busyKey.value = '@all';
  let ok = 0, fail = 0, firstError = '';
  try {
    for (const row of rows.value) {
      const name = row.name.trim();
      if (!name) continue;
      const live = liveOf(row);
      if (live) {
        if (live.ready) { ok++; continue; }
        const j = await API.dummyReady(live.name, true);
        if (j.ok) { ok++; await refreshSoon(); }
        else { fail++; if (!firstError) firstError = j.data?.message || j.error || ''; }
      } else {
        const j = await API.dummyCreate(name, row.characterId);
        if (!j.ok) { fail++; if (!firstError) firstError = j.data?.message || j.error || ''; continue; }
        const rj = await API.dummyReady(name, true);
        if (rj.ok) { ok++; await refreshSoon(); }
        else { fail++; if (!firstError) firstError = rj.data?.message || rj.error || ''; }
      }
    }
    if (fail) showToast(`一键准备：${ok} 成功，${fail} 失败（${firstError}）`, true);
    else if (ok) showToast(`一键准备完成：${ok} 个假人就绪`);
    else showToast('没有需要准备的行');
  } finally {
    busyKey.value = '';
  }
}

/**
 * 一键取消准备：遍历已进场且已准备的行，取消就绪。失败不中断，汇总 toast。
 */
async function unreadyAll() {
  if (busyKey.value) return;
  busyKey.value = '@all';
  let ok = 0, fail = 0, firstError = '';
  try {
    for (const row of rows.value) {
      const live = liveOf(row);
      if (!live || !live.ready) continue;
      const j = await API.dummyReady(live.name, false);
      if (j.ok) { ok++; await refreshSoon(); }
      else { fail++; if (!firstError) firstError = j.data?.message || j.error || ''; }
    }
    if (fail) showToast(`一键取消准备：${ok} 成功，${fail} 失败（${firstError}）`, true);
    else if (ok) showToast(`一键取消准备完成：${ok} 个假人`);
    else showToast('没有已准备的假人');
  } finally {
    busyKey.value = '';
  }
}

/**
 * 一键选角：遍历已进场的行，按各自所选角色选角（选角阶段锁定 / 大厅换装）。
 * 大厅换角会自动取消准备（游戏规则），选完后如需开局再点一键准备。
 */
async function pickAll() {
  if (busyKey.value) return;
  busyKey.value = '@all';
  let ok = 0, fail = 0, skipped = 0, firstError = '';
  try {
    for (const row of rows.value) {
      const live = liveOf(row);
      if (!live) { skipped++; continue; }
      const j = await API.dummyPick(live.name, row.characterId);
      if (j.ok) ok++;
      else { fail++; if (!firstError) firstError = j.data?.message || j.error || ''; }
    }
    if (fail) showToast(`一键选角：${ok} 成功，${fail} 失败（${firstError}）`, true);
    else if (ok) showToast(`一键选角完成：${ok} 个假人已锁定角色`);
    else showToast('没有已进场的假人（未进场的行已跳过）');
  } finally {
    busyKey.value = '';
  }
}

/** 批量操作中途刷一次状态，让下一行的 liveOf 尽量新鲜（失败重试也走最新快照）。 */
async function refreshSoon() {
  await reload();
}

onMounted(() => {
  loadCharacters();
  poller.start();
});
onUnmounted(poller.stop);
</script>

<template>
  <div class="dummy-app">
    <div class="toolbar">
      <div class="toolbar-left">
        <span class="chip" :class="{ on: isHost }">{{ isHost ? '房主' : '非房主' }}</span>
        <span class="chip" :class="{ on: gameState === 'Lobby' || gameState === 'PickCharacter' }">{{ stateText }}</span>
        <span class="chip" :class="{ on: liveCount > 0 }">假人 {{ liveCount }}/{{ usableCapacity }}</span>
      </div>
      <div class="toolbar-right">
        <button
          class="tb-btn"
          :disabled="busyKey !== '' || !canOperate"
          title="全部行：未进场的创建并就绪，已进场未准备的补就绪"
          @click="prepareAll"
        ><IconUserCheck /> 一键准备</button>
        <button
          class="tb-btn"
          :disabled="busyKey !== '' || !canOperate"
          title="全部已进场行：取消就绪"
          @click="unreadyAll"
        ><IconUserMinus /> 一键取消准备</button>
        <button
          class="tb-btn"
          :disabled="busyKey !== '' || !canPick"
          title="全部已进场行：按各自所选角色选角（选角阶段锁定 / 大厅换装）"
          @click="pickAll"
        ><IconListCheck /> 一键选角</button>
        <button class="tb-btn" :disabled="busyKey !== ''" title="移出房间里全部假人（行配置保留）" @click="removeAll">
          <IconEraser /> 全部移出
        </button>
        <button class="tb-btn" title="立即刷新" @click="reload"><IconRefresh /> 刷新</button>
      </div>
    </div>

    <div class="rows">
      <div v-for="row in rows" :key="row.id" class="row" :class="{ live: liveOf(row) }">
        <input
          v-model="row.name"
          class="name-input"
          :disabled="!!liveOf(row)"
          maxlength="64"
          placeholder="假人昵称"
          @keydown.enter="prepare(row)"
        >
        <select v-model.number="row.characterId" class="cfg-select" title="选择角色（随机 = 让游戏分配空闲角色）">
          <option v-for="c in characters" :key="c.id" :value="c.id">{{ c.label }}</option>
        </select>

        <span v-if="liveOf(row)" class="live-badge" :title="`#${liveOf(row).playerId} · 角色 ${charLabel(liveOf(row).characterId)}`">
          #{{ liveOf(row).playerId }}
        </span>

        <button
          class="tb-btn action"
          :class="{ ready: liveOf(row)?.ready }"
          :disabled="busyKey !== '' || !canOperate"
          :title="liveOf(row) ? '切换就绪态' : '创建假人并自动准备'"
          @click="prepare(row)"
        >{{ liveOf(row)?.ready ? '取消准备' : '准备' }}</button>

        <button
          class="tb-btn action"
          :disabled="busyKey !== '' || !canPick || !liveOf(row)"
          title="让假人使用左侧所选角色（选角阶段锁定 / 大厅换装）"
          @click="pick(row)"
        >选角</button>

        <button class="icon-btn" :disabled="busyKey !== ''" title="删除该行（若已进场会先移出假人）" @click="removeRow(row)">
          <IconX />
        </button>
      </div>

      <button class="add-btn" :disabled="rows.length >= usableCapacity" @click="addRow">
        <IconPlus /> 添加假人
      </button>
    </div>

    <div v-if="toast.text" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <div class="foot">
      <span v-if="loadError" class="foot-err">{{ loadError }}</span>
      <template v-else>
        <span v-if="inPickPhase"><IconChecks /> 选角阶段：点「选角」为假人锁定角色（随机可抢锁定）。</span>
        <span v-else>「准备」= 创建假人进场并自动准备；大厅换角会自动取消准备；回大厅后游戏自动清场，行配置保留。</span>
      </template>
    </div>
  </div>
</template>

<style scoped>
.dummy-app { display: flex; flex-direction: column; height: 100%; background: var(--surface-0); position: relative; }

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 10px 14px;
  flex-shrink: 0;
  flex-wrap: wrap;
  background: var(--surface-1);
  border-bottom: 1px solid var(--line);
}
.toolbar-left { display: flex; align-items: center; gap: 6px; }
.toolbar-right { display: flex; align-items: center; gap: 6px; }

.chip {
  font-size: 11px;
  color: var(--text-2);
  border: 1px solid var(--line);
  border-radius: 999px;
  padding: 2px 9px;
  white-space: nowrap;
}
.chip.on { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }

.tb-btn {
  display: flex; align-items: center; gap: 5px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 11.5px;
  padding: 6px 10px;
  cursor: pointer;
  white-space: nowrap;
}
.tb-btn svg { width: 13px; height: 13px; }
.tb-btn:hover:not(:disabled) { color: var(--text-0); border-color: var(--line-strong); }
.tb-btn:disabled { opacity: 0.45; cursor: default; }
.tb-btn.ready { color: var(--accent-green); border-color: rgba(71, 212, 185, 0.4); }

.rows { flex: 1; overflow-y: auto; padding: 12px 14px; display: flex; flex-direction: column; gap: 8px; }

.row {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--surface-1);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  padding: 8px 10px;
}
.row.live { border-color: var(--accent-cyan-dim); }

.name-input {
  flex: 1.2;
  min-width: 90px;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12.5px;
  padding: 7px 10px;
  outline: none;
}
.name-input:focus { border-color: var(--accent-cyan-dim); }
.name-input:disabled { color: var(--text-2); }

.cfg-select {
  flex: 1.6;
  min-width: 130px;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12.5px;
  padding: 7px 8px;
  outline: none;
  cursor: pointer;
}
.cfg-select:focus { border-color: var(--accent-cyan-dim); }
.cfg-select:disabled { opacity: 0.6; cursor: default; }

.live-badge {
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--accent-cyan);
  border: 1px solid var(--accent-cyan-dim);
  border-radius: 4px;
  padding: 2px 6px;
  white-space: nowrap;
}
.row.live .action:not(:disabled) { border-color: var(--line-strong); }

.icon-btn {
  width: 30px; height: 30px;
  display: grid; place-items: center;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
  flex-shrink: 0;
}
.icon-btn:hover:not(:disabled) { color: var(--accent-red); border-color: var(--accent-red); }
.icon-btn:disabled { opacity: 0.45; cursor: default; }
.icon-btn svg { width: 13px; height: 13px; }

.add-btn {
  display: flex; align-items: center; justify-content: center; gap: 6px;
  border: 1px dashed var(--line-strong);
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-2);
  font-size: 12.5px;
  padding: 10px 0;
  cursor: pointer;
}
.add-btn svg { width: 14px; height: 14px; }
.add-btn:hover:not(:disabled) { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.add-btn:disabled { opacity: 0.45; cursor: default; }

.toast {
  position: absolute; top: 56px; right: 16px;
  background: var(--surface-2);
  border: 1px solid var(--accent-cyan-dim);
  border-radius: var(--radius-sm);
  padding: 8px 14px;
  font-size: 12px;
  color: var(--text-0);
  z-index: 100;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
  max-width: 70%;
}
.toast.error { border-color: var(--accent-red); color: var(--accent-red); }

.foot {
  flex-shrink: 0;
  border-top: 1px solid var(--line);
  background: var(--surface-1);
  padding: 7px 14px;
  font-size: 11px;
  color: var(--text-2);
  display: flex; align-items: center; gap: 6px;
}
.foot svg { width: 12px; height: 12px; color: var(--accent-cyan); flex-shrink: 0; }
.foot-err { color: var(--accent-red); }
</style>
