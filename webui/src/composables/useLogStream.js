import { useConnectionStatus } from './useConnectionStatus.js';
import { API } from '../api.js';

const conn = useConnectionStatus();
const subs = new Set();
const buffer = [];
let socket = null;
let seq = 0;
let entryId = 0;
let started = false;
let retryTimer = null;
let retryDelay = 1000;
let pollTimer = null;
let wsFailures = 0;
let wsPort = null;
let metaPending = false;

const RETRY_MAX = 8000;
const WS_FALLBACK_AFTER = 2;
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
    if (typeof e.seq !== 'number' || e.seq <= seq) continue;
    seq = e.seq;
    dispatch({ ...e, id: ++entryId });
  }
}

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

async function discoverPort() {
  if (metaPending) return;
  metaPending = true;
  try {
    const m = await API.meta();
    if (m && m.ok && Number.isInteger(m.wsPort) && m.wsPort >= 0) wsPort = m.wsPort;
  } catch { }
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
  if (wsPort === 0) { startPolling(); return; }
  try {
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
      return;
    }
    if (!Array.isArray(data)) return;
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
    startPolling();
    return;
  }
  scheduleRetry();
}

export function useLogStream() {
  if (!started) {
    started = true;
    discoverPort().then(() => { if (started && !pollTimer) connect(); });
  }
  return {
    onEntry(fn) {
      subs.add(fn);
      return () => subs.delete(fn);
    },
    replay(fn) {
      for (const e of buffer) fn(e);
    },
  };
}
