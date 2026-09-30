import { useConnectionStatus } from './useConnectionStatus.js';
import { API } from '../api.js';

/**
 * 全局日志流单例：优先 WebSocket（独立端口 /api/log/ws，经 /api/meta 发现），失败自动降级轮询。
 *
 * - WS：Unity Mono 的 HttpListener 不支持 WS 升级（AcceptWebSocketAsync 未实现，实机确认），
 *   插件改由自管 TCP 通道在独立端口（WsPort，/api/meta 下发）提供真 WS；握手 URL 带
 *   ?since=<seq> 续传，服务端按 seq 去重无漏无重，15s 无增量推 {"t":"ping"} 心跳
 *   （同时就是游戏存活探测）。
 * - 降级：插件禁用实时流（wsPort=0）→ 直接轮询；旧插件无 /api/meta → 同源握手
 *   连续 2 次失败后切换轮询 /api/log?since=seq（0.8s 增量，功能等价），不再反复撞 WS。
 * - 条目形状：{ seq, time, level, tag, msg, session }；session=null 表示全局日志。
 * - 全量历史有界缓冲：晚打开的窗口经 replay() 补齐打开之前的日志。
 * - 在线状态写入 useConnectionStatus，顶栏照常展示。
 */
const conn = useConnectionStatus();
const subs = new Set();
const buffer = [];            // 全量历史（有界）：晚打开的窗口经 replay() 补齐打开前的日志
let socket = null;
let seq = 0;
let entryId = 0;
let started = false;
let retryTimer = null;
let retryDelay = 1000;
let pollTimer = null;
let wsFailures = 0;
let wsPort = null;            // /api/meta 探测到的独立 WS 端口；null=未知（走同源旧路径）
let metaPending = false;

const RETRY_MAX = 8000;
const WS_FALLBACK_AFTER = 2;   // 连续失败次数达到即降级轮询
const POLL_MS = 800;
const BUFFER_MAX = 5000;
const BUFFER_TRIM = 1000;

function dispatch(entry) {
  buffer.push(entry);
  if (buffer.length > BUFFER_MAX) buffer.splice(0, BUFFER_TRIM);
  for (const fn of subs) fn(entry);
}

function acceptEntries(list) {
  for (const e of list) {
    if (typeof e.seq !== 'number' || e.seq <= seq) continue;   // 重放/实时交界去重
    seq = e.seq;
    dispatch({ ...e, id: ++entryId });
  }
}

// ── 轮询兜底（Mono 无 WS 升级能力时的等价通道）──

function startPolling() {
  if (pollTimer) return;
  pollTimer = setInterval(poll, POLL_MS);
  poll();
}

async function poll() {
  if (!started || document.hidden) return;
  try {
    const data = await API.logSince(seq);
    if (data.unauthorized || data.offline || !Array.isArray(data)) {
      conn.online = false;
      return;
    }
    conn.online = true;
    acceptEntries(data);
  } catch {
    conn.online = false;
  }
}

// ── WebSocket 主通道 ──

/**
 * 端口发现：/api/meta 返回插件自管 WS 通道的独立端口（Mono 的 HttpListener
 * 不支持升级）。旧插件没有该端点（404）→ 保持 null（回退同源旧路径，撞 501
 * 再降级轮询）；wsPort=0（插件禁用实时流）→ 存 0（connect 直接降级轮询）。
 */
async function discoverPort() {
  if (metaPending) return;
  metaPending = true;
  try {
    const m = await API.meta();
    if (m && m.ok && Number.isInteger(m.wsPort) && m.wsPort >= 0) wsPort = m.wsPort;
  } catch { /* 离线/旧插件：保持 null */ }
}

function scheduleRetry() {
  if (retryTimer || !started) return;
  retryTimer = setTimeout(() => {
    retryTimer = null;
    connect();
  }, retryDelay);
  retryDelay = Math.min(retryDelay * 2, RETRY_MAX);
}

function connect() {
  if (!started || pollTimer || (socket && socket.readyState <= WebSocket.OPEN)) return;
  if (wsPort === 0) { startPolling(); return; }   // 插件明确禁用实时流：不再撞握手
  try {
    // since 附在握手 URL 上（后端从 query 读），重连只补缺口
    socket = new WebSocket(API.wsLogUrl(wsPort) + '?since=' + seq);
  } catch {
    wsFailed();
    return;
  }
  socket.onopen = () => {
    conn.online = true;
    retryDelay = 1000;
    wsFailures = 0;
  };
  socket.onmessage = (ev) => {
    let data;
    try {
      data = JSON.parse(ev.data);
    } catch {
      return;   // 非法帧忽略
    }
    if (!Array.isArray(data)) return;   // {"t":"ping"} 心跳帧
    conn.online = true;
    acceptEntries(data);
  };
  socket.onerror = () => {
    conn.online = false;
  };
  socket.onclose = () => {
    conn.online = false;
    socket = null;
    wsFailed();
  };
}

function wsFailed() {
  wsFailures++;
  if (wsFailures >= WS_FALLBACK_AFTER) {
    // 运行时大概率不支持 WS 升级（Mono）：本会话固定走轮询，不再反复撞握手
    startPolling();
    return;
  }
  scheduleRetry();
}

/** 启动全局流（幂等）；Desktop 挂载即调用，不依赖任何窗口是否打开。 */
export function useLogStream() {
  if (!started) {
    started = true;
    // 先做端口发现再连 WS（旧插件无 /api/meta 时 discoverPort 立即返回，走同源旧路径）
    discoverPort().then(() => { if (started && !pollTimer) connect(); });
  }
  return {
    /**
     * 订阅实时日志条目；返回退订函数。回调按流原序收到
     * { seq,time,level,tag,msg,session,id }。
     */
    onEntry(fn) {
      subs.add(fn);
      return () => subs.delete(fn);
    },
    /**
     * 把缓冲的历史条目立即按序回放给 fn（配合 onEntry 用同一个 fn：
     * 先 replay 再 onEntry，两者同步执行于同一 tick，中间不会有事件插入——
     * 晚打开的窗口因此能看到打开之前的日志）。
     */
    replay(fn) {
      for (const e of buffer) fn(e);
    },
  };
}
