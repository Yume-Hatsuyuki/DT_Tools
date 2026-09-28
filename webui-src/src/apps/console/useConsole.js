import { reactive, ref } from 'vue';
import { API, createPoller } from '../../api.js';
import { useConnectionStatus } from '../../composables/useConnectionStatus.js';

const MAX_ENTRIES = 2000;
const HISTORY_KEY = 'dt_console_history';
const HISTORY_MAX = 100;

const colorMap = {
  red: 'var(--accent-red)',
  orange: 'var(--accent-amber)',
  cyan: 'var(--accent-cyan)',
  white: 'var(--text-0)',
};

/**
 * 控制台状态：单例（跨窗口开关关闭再打开也保留日志/历史），逻辑照搬旧版
 * console.js（SSE 优先失败降级轮询、命令补全、历史记录、JSON 折叠展示），
 * 只是把 DOM 操作换成响应式数组交给模板渲染。
 */
const entries = reactive([]);   // { id, time, color, text }
let seq = 0;
let entryId = 0;
let started = false;
const conn = useConnectionStatus();

function pushEntry(time, color, text) {
  entries.push({ id: ++entryId, time, color, text });
  while (entries.length > MAX_ENTRIES) entries.shift();
}

function appendServerEntry(e) {
  if (typeof e.seq === 'number' && e.seq > seq) seq = e.seq;
  pushEntry(e.time || '', colorMap[e.color] || 'var(--text-0)', e.msg);
}

function appendLocal(text, color) {
  pushEntry('', color || 'var(--text-0)', text);
}

function appendResult(r) {
  if (r && r.ok === false) {
    pushEntry('', 'var(--accent-red)', '✕ ' + (r.error || '执行失败'));
  } else if (r && r.data != null) {
    entries.push({
      id: ++entryId,
      time: '',
      color: null,
      json: JSON.stringify(r.data, null, 2),
    });
    while (entries.length > MAX_ENTRIES) entries.shift();
  }
  // ok 且无数据：维持原样不输出
}

function startPolling() {
  createPoller(async () => {
    const data = await API.logSince(seq);
    if (data.unauthorized || data.offline) { conn.online = false; return; }
    if (!Array.isArray(data)) { conn.online = false; return; }
    conn.online = true;
    data.forEach(appendServerEntry);
  }, 800).start();
}

function startLogSource() {
  if (typeof EventSource === 'undefined') { startPolling(); return; }
  const es = new EventSource(API.logStreamUrl);

  // 看门狗只看 EventSource.readyState：服务端无增量时发的是 SSE 注释行心跳
  // （": ping"），浏览器不会为注释行派发 message 事件——若按"多久没消息"判断，
  // 静默期会被误判成离线（任务栏永远"连接中…"的根源）。OPEN 即在线，
  // 服务端断开后浏览器进入自动重连（CONNECTING）或放弃（CLOSED），状态自然翻转。
  const watchdog = setInterval(() => {
    if (es.readyState === EventSource.CLOSED) {
      clearInterval(watchdog);
      es.close();
      startPolling();
      return;
    }
    conn.online = es.readyState === EventSource.OPEN;
  }, 2000);

  es.onopen = () => { conn.online = true; };
  es.onmessage = (ev) => {
    try {
      const batch = JSON.parse(ev.data);
      if (Array.isArray(batch)) batch.forEach(appendServerEntry);
    } catch { /* 半包忽略，下一批自愈 */ }
    conn.online = true;
  };
  es.onerror = () => {
    conn.online = false;
    if (es.readyState === EventSource.CLOSED) {
      clearInterval(watchdog);
      es.close();
      startPolling();
    }
  };
}

function loadHistory() {
  try {
    const raw = localStorage.getItem(HISTORY_KEY);
    const hist = raw ? JSON.parse(raw) : [];
    return Array.isArray(hist) ? hist : [];
  } catch { return []; }
}

const history = ref(loadHistory());
let historyIndex = history.value.length;

function pushHistory(v) {
  history.value.push(v);
  if (history.value.length > HISTORY_MAX) history.value = history.value.slice(-HISTORY_MAX);
  historyIndex = history.value.length;
  try { localStorage.setItem(HISTORY_KEY, JSON.stringify(history.value)); } catch { /* 隐私模式忽略 */ }
}

function historyUp(current) {
  if (history.value.length && historyIndex > 0) {
    historyIndex--;
    return history.value[historyIndex];
  }
  return current;
}
function historyDown() {
  if (historyIndex < history.value.length - 1) {
    historyIndex++;
    return history.value[historyIndex];
  }
  historyIndex = history.value.length;
  return '';
}

const commands = ref([]);
async function loadCommands() {
  const list = await API.commands();
  commands.value = Array.isArray(list) ? list : [];
}

async function send(raw) {
  const v = raw.trim();
  if (!v) return;
  pushHistory(v);
  appendLocal('> ' + v, 'var(--accent-cyan)');
  const r = await API.run(v);
  if (r.unauthorized) return;
  appendResult(r);
}

/** 终端本地输出（内置命令用，不进后端日志）。支持多行文本。 */
function print(text, color) {
  for (const line of String(text).split('\n'))
    pushEntry('', color || 'var(--text-0)', line);
}

/** 终端清屏：只清本地视图，不动服务端环形缓冲。 */
function clearEntries() {
  entries.splice(0, entries.length);
}

export function useConsole() {
  if (!started) {
    started = true;
    startLogSource();
    loadCommands();
  }
  return { entries, commands, conn, send, print, clearEntries, historyUp, historyDown };
}
