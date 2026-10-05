<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { API } from '../api.js';
import IconCheck from '~icons/tabler/check';
import IconCopy from '~icons/tabler/copy';
import IconX from '~icons/tabler/x';

/**
 * MCP 桥接小组件（插件式弹窗，Steam 在线同款）：可拖动、可关闭，Dock 重开。
 * 展示桥接状态（启用/停用）、端点 URL、通用 MCP 接入配置片段（一键复制）、工具计数与
 * 调用滚动日志（后端环形缓冲，随轮询流入）；配置区（端口/监听/密码）与「DT 配置」页读写
 * 同一份 Mcp 段（都走 ConfigService），
 * 两处天然同步——本组件 5 秒轮询拉回对方改动，未聚焦的字段自动跟随。
 * 端口/监听/密码为监听参数，热改不重绑，应用后提示重启生效。
 */
const emit = defineEmits(['close']);

const STORE_KEY = 'dt_mcp_widget_v1';
const TOPBAR_H = 32;

/** 配置区字段 → Mcp 段配置键（与引擎推导键名逐字一致）。 */
const CFG_KEYS = { port: 'Port', listenIp: 'ListenIp', password: 'Password' };

function load() {
  try { return JSON.parse(localStorage.getItem(STORE_KEY)) || {}; } catch { return {}; }
}

const pos = ref(load().pos || null);
const rootEl = ref(null);
let timer = null;
const status = ref(null);   // {ok,enabled,running,url,port,listenIp,password,callCount,lastCall,calls,tools}
const copied = ref(false);
const cfg = ref({ port: '', listenIp: '', password: '' });   // 编辑缓冲
const focusedKey = ref(null);
const restartHint = ref(false);

function persist(closed) {
  try {
    localStorage.setItem(STORE_KEY, JSON.stringify({ closed, pos: pos.value }));
  } catch { /* 隐私模式忽略 */ }
}

onMounted(() => {
  persist(false);
  refresh();
  timer = setInterval(refresh, 5000);
});
onUnmounted(() => {
  clearInterval(timer);
  window.removeEventListener('pointermove', onMove);
  window.removeEventListener('pointerup', onUp);
});

async function refresh() {
  const s = await API.mcpStatus();
  status.value = s;
  // 跟随另一处（DT 配置页/其他挂件实例）的改动；正在编辑的字段不覆盖
  if (s) {
    if (focusedKey.value !== 'port') cfg.value.port = s.port;
    if (focusedKey.value !== 'listenIp') cfg.value.listenIp = s.listenIp;
    if (focusedKey.value !== 'password') cfg.value.password = s.password;
  }
}

/** 失焦时若与后端值不同才应用（写 Mcp 段 + 落盘），避免轮询期间空写。 */
async function onBlur(key) {
  focusedKey.value = null;
  const raw = String(cfg.value[key] ?? '');
  const current = key === 'port' ? status.value?.port : key === 'listenIp' ? status.value?.listenIp : status.value?.password;
  if (String(current ?? '') === raw) return;
  const res = await API.configUpdate('Mcp', CFG_KEYS[key], raw);
  if (res && res.ok === false) {
    await refresh();
    return;
  }
  await API.configSave();
  restartHint.value = true;
  await refresh();
}

async function toggle() {
  await API.mcpToggle(!(status.value?.enabled));
  await refresh();
}

function close() {
  persist(true);
  clearInterval(timer);
  emit('close');
}

const enabled = computed(() => !!status.value?.enabled);
const listenerOff = computed(() => enabled.value && status.value && !status.value.running);
const toolCount = computed(() => status.value?.tools?.length ?? 0);

const snippet = computed(() => JSON.stringify({
  mcp: { servers: { 'dt-tools': { type: 'http', url: status.value?.url || 'http://127.0.0.1:19452/mcp' } } },
}, null, 2));

async function copySnippet() {
  try {
    await navigator.clipboard.writeText(snippet.value);
    copied.value = true;
    setTimeout(() => { copied.value = false; }, 1600);
  } catch { /* 剪贴板不可用时静默 */ }
}

const posStyle = computed(() =>
  pos.value ? { left: pos.value.left + 'px', top: pos.value.top + 'px', right: 'auto' } : {});

// ---- 标题栏拖动（视口内钳位，SteamWidget 同款） ----

let drag = null;

function onHeadDown(e) {
  if (e.target.closest('button, input')) return;
  const r = rootEl.value.getBoundingClientRect();
  drag = { dx: e.clientX - r.left, dy: e.clientY - r.top };
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}
function onMove(e) {
  const el = rootEl.value;
  const left = Math.min(Math.max(8, e.clientX - drag.dx), window.innerWidth - el.offsetWidth - 8);
  const top = Math.min(Math.max(TOPBAR_H + 8, e.clientY - drag.dy), window.innerHeight - el.offsetHeight - 8);
  pos.value = { left, top };
}
function onUp() {
  drag = null;
  window.removeEventListener('pointermove', onMove);
  window.removeEventListener('pointerup', onUp);
  persist(false);
}
</script>

<template>
  <div ref="rootEl" class="mcp-widget" :class="enabled ? 'ok' : 'off'" :style="posStyle">
    <div class="mw-head" @pointerdown="onHeadDown">
      <span class="mw-title">MCP 桥接（AI 接入）</span>
      <button class="mw-close" title="关闭（可从底部 Dock 再次打开）" @click="close"><IconX /></button>
    </div>
    <div class="mw-body">
      <div class="mw-row">
        <span class="mw-state" :class="{ on: enabled }">{{ enabled ? '运行中' : '已停用' }}</span>
        <button class="mw-toggle" @click="toggle">{{ enabled ? '停用' : '启用' }}</button>
      </div>
      <div class="mw-sub">{{ status?.url || 'http://127.0.0.1:19452/mcp' }}</div>
      <button class="mw-copy" @click="copySnippet">
        <IconCheck v-if="copied" /><IconCopy v-else />{{ copied ? '已复制' : '复制接入配置' }}
      </button>

      <div class="mw-cfg-title">监听配置（与「DT 配置」页同步）</div>
      <div class="mw-cfg">
        <label class="mw-field mw-field-port">
          <span>端口</span>
          <input
            v-model="cfg.port" type="text" inputmode="numeric"
            @focus="focusedKey = 'port'" @blur="onBlur('port')" @keyup.enter="$event.target.blur()">
        </label>
        <label class="mw-field">
          <span>监听 IP</span>
          <input
            v-model="cfg.listenIp" type="text" spellcheck="false"
            @focus="focusedKey = 'listenIp'" @blur="onBlur('listenIp')" @keyup.enter="$event.target.blur()">
        </label>
        <label class="mw-field">
          <span>密码</span>
          <input
            v-model="cfg.password" type="password" spellcheck="false" placeholder="空=免密"
            @focus="focusedKey = 'password'" @blur="onBlur('password')" @keyup.enter="$event.target.blur()">
        </label>
      </div>
      <div v-if="restartHint" class="mw-hint">监听参数已改，重启游戏后生效</div>
      <div v-if="listenerOff" class="mw-hint">监听未拉起（启动时已停用）：可经 WebConsole 端口 /mcp 接入，或重启游戏</div>

      <div class="mw-stats">
        <div class="mw-line">工具 {{ toolCount }} 个 · 调用 {{ status?.callCount ?? 0 }} 次</div>
        <div class="mw-log">
          <div v-if="!(status?.calls?.length)" class="mw-log-empty">暂无调用（AI 每次工具调用都会记录在此）</div>
          <div
            v-for="(c, i) in status?.calls || []" :key="status?.callCount + '-' + i"
            class="mw-call" :class="{ fail: !c.ok }" :title="c.args || c.name">
            <div class="mw-call-meta">
              <span class="mw-call-t">{{ c.t }}</span>
              <span class="mw-call-name">{{ c.name }}</span>
              <span class="mw-call-ms">{{ c.ok ? c.ms + 'ms' : (c.error || 'fail') }}</span>
            </div>
            <div v-if="c.args" class="mw-call-args">{{ c.args }}</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.mcp-widget {
  position: fixed;
  z-index: 4500;
  width: 236px;
  /* 默认悬停右上角 SteamWidget 的下方；拖动后由内联 left/top 接管 */
  top: calc(var(--topbar-h) + 140px);
  right: 14px;
  border-radius: 14px;
  background: rgba(9, 18, 26, 0.78);
  backdrop-filter: blur(20px) saturate(150%);
  border: 1px solid var(--dock-border);
  box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7);
  user-select: none;
}
.mcp-widget.ok {
  border-color: rgba(71, 212, 185, 0.55);
  box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7), 0 0 14px rgba(71, 212, 185, 0.28);
}
.mcp-widget.off { border-color: rgba(255, 82, 82, 0.45); }

.mw-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 8px 6px 12px;
  cursor: grab;
}
.mcp-widget:active .mw-head { cursor: grabbing; }
.mw-title {
  flex: 1;
  font-size: 11px;
  color: var(--text-2);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.mw-close {
  width: 20px;
  height: 20px;
  display: grid;
  place-items: center;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
}
.mw-close:hover { background: rgba(255, 82, 82, 0.15); color: var(--accent-red); }
.mw-close svg { width: 12px; height: 12px; }

.mw-body { padding: 2px 14px 12px; }
.mw-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.mw-state {
  font-family: var(--font-mono);
  font-size: 15px;
  font-weight: 700;
  color: var(--accent-red);
}
.mw-state.on { color: var(--accent-green); text-shadow: 0 0 12px rgba(71, 212, 185, 0.5); }
.mw-toggle {
  background: rgba(150, 225, 255, 0.10);
  border: 1px solid var(--dock-border);
  border-radius: 7px;
  color: var(--text-1);
  font-size: 11px;
  padding: 3px 10px;
  cursor: pointer;
}
.mw-toggle:hover { color: var(--text-0); background: rgba(150, 225, 255, 0.16); }
.mw-sub {
  font-family: var(--font-mono);
  font-size: 10.5px;
  color: var(--text-1);
  margin-top: 6px;
  word-break: break-all;
}
.mw-copy {
  margin-top: 6px;
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  background: rgba(150, 225, 255, 0.10);
  border: 1px solid var(--dock-border);
  border-radius: 7px;
  color: var(--text-1);
  font-size: 11px;
  padding: 5px 0;
  cursor: pointer;
}
.mw-copy:hover { color: var(--text-0); background: rgba(150, 225, 255, 0.16); }
.mw-copy svg { width: 12px; height: 12px; }

.mw-cfg-title { font-size: 10.5px; color: var(--text-2); margin-top: 10px; }
.mw-cfg { display: flex; flex-direction: column; gap: 5px; margin-top: 4px; }
.mw-field { display: flex; align-items: center; gap: 6px; }
.mw-field span { font-size: 10.5px; color: var(--text-2); width: 44px; flex-shrink: 0; }
.mw-field input {
  flex: 1;
  min-width: 0;
  background: rgba(4, 12, 18, 0.55);
  border: 1px solid var(--dock-border);
  border-radius: 6px;
  color: var(--text-0);
  font-family: var(--font-mono);
  font-size: 10.5px;
  padding: 4px 7px;
  outline: none;
}
.mw-field input:focus { border-color: rgba(150, 225, 255, 0.45); }
.mw-hint { font-size: 10px; color: var(--accent-red); margin-top: 6px; line-height: 1.5; }

.mw-stats { margin-top: 10px; }
.mw-line { font-size: 10.5px; color: var(--text-1); line-height: 1.7; }

/* 调用滚动日志：后端环形缓冲 30 条，5 秒轮询自然流入；单条=元信息行+参数行 */
.mw-log {
  margin-top: 5px;
  max-height: 150px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 3px;
  scrollbar-width: thin;
}
.mw-log-empty {
  font-size: 10px;
  color: var(--text-2);
  background: rgba(4, 12, 18, 0.35);
  border: 1px dashed var(--dock-border);
  border-radius: 6px;
  padding: 6px 8px;
  line-height: 1.5;
}
.mw-call {
  background: rgba(4, 12, 18, 0.45);
  border: 1px solid rgba(255, 255, 255, 0.04);
  border-radius: 5px;
  padding: 3px 6px;
}
.mw-call.fail { border-color: rgba(255, 82, 82, 0.35); }
.mw-call-meta { display: flex; align-items: baseline; gap: 6px; min-width: 0; }
.mw-call-t { font-family: var(--font-mono); font-size: 9.5px; color: var(--text-2); flex-shrink: 0; }
.mw-call-name {
  flex: 1;
  font-family: var(--font-mono);
  font-size: 10.5px;
  color: var(--text-0);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.mw-call-ms { font-family: var(--font-mono); font-size: 9.5px; color: var(--accent-green); flex-shrink: 0; }
.mw-call.fail .mw-call-ms { color: var(--accent-red); }
.mw-call-args {
  font-family: var(--font-mono);
  font-size: 9.5px;
  color: var(--text-2);
  margin-top: 1px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
