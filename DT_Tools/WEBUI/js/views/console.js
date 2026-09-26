/**
 * 控制台视图：日志流（SSE 优先，失败降级轮询）+ 命令补全/历史 + 结果信封展示。
 * 日志 DOM 上限 2000 条，超出滚动丢弃最旧（seq 不受影响）。
 */

import { API, createPoller } from '../api.js';
import { esc, toast } from '../ui.js';

const MAX_DOM_ENTRIES = 2000;
const HISTORY_KEY = 'dt_console_history';
const HISTORY_MAX = 100;

const colorMap = {
  red: 'var(--red)',
  orange: 'var(--amber)',
  cyan: 'var(--cyan)',
  white: 'var(--text)',
};

let seq = 0;
let logEl, inp, sugEl, statusEl, statusText;
let commands = [];
let matches = [];
let selIdx = -1;
let renderedKey = '';
let hist = [];
let hIdx = -1;

/* ── 日志渲染 ──────────────────────────────── */

function setOnline(ok) {
  if (!statusEl) return;
  statusEl.classList.toggle('online', ok);
  statusEl.classList.toggle('offline', !ok);
  if (statusText) statusText.textContent = ok ? '在线' : '离线';
}

function appendEntry(e) {
  if (typeof e.seq === 'number' && e.seq > seq) seq = e.seq;
  const d = document.createElement('div');
  d.className = 'entry';
  // msg 含游戏文本，必须转义后才能进 innerHTML
  d.innerHTML =
    '<span class="ts">' + esc(e.time || '') + '</span>' +
    '<span style="color:' + (colorMap[e.color] || 'var(--text)') + '">' + esc(e.msg) + '</span>';
  logEl.appendChild(d);
  while (logEl.childElementCount > MAX_DOM_ENTRIES)
    logEl.firstElementChild.remove();
  logEl.scrollTop = logEl.scrollHeight;
}

function appendLocal(msg, color) {
  const d = document.createElement('div');
  d.className = 'entry';
  d.style.color = color || 'var(--text)';
  d.textContent = msg;
  logEl.appendChild(d);
  while (logEl.childElementCount > MAX_DOM_ENTRIES)
    logEl.firstElementChild.remove();
  logEl.scrollTop = logEl.scrollHeight;
}

/**
 * 命令结果行：数据体（如 /help 的整表 JSON）收进可折叠块——
 * 摘要计字符数，展开后语法上色 + 只读可选中 + 一键复制。
 */
function appendResult(r) {
  const d = document.createElement('div');
  d.className = 'entry';
  if (r && r.ok === false) {
    d.style.color = 'var(--red)';
    d.textContent = '✕ ' + (r.error || '执行失败');
  } else if (r && r.data != null) {
    const text = JSON.stringify(r.data, null, 2);
    const fold = document.createElement('details');
    fold.className = 'json-fold';
    const sum = document.createElement('summary');
    sum.textContent = 'json响应（' + text.length + ' 字符，点击展开）';

    const bar = document.createElement('div');
    bar.className = 'json-copy-bar';
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'btn secondary btn-xs';
    btn.textContent = '复制 JSON';
    btn.onclick = async () => {
      try { await navigator.clipboard.writeText(text); toast('JSON 已复制'); }
      catch { toast('复制失败（浏览器拒绝剪贴板）', true); }
    };
    bar.appendChild(btn);

    const pre = document.createElement('pre');
    pre.className = 'json-body';
    appendJsonHighlighted(pre, text);   // 动态文本全部走 textContent/文本节点

    fold.appendChild(sum);
    fold.appendChild(bar);
    fold.appendChild(pre);
    d.appendChild(fold);
  } else {
    return; // ok 且无数据：维持原样不输出
  }
  logEl.appendChild(d);
  while (logEl.childElementCount > MAX_DOM_ENTRIES)
    logEl.firstElementChild.remove();
  logEl.scrollTop = logEl.scrollHeight;
}

/** JSON 语法上色：key/string/number/bool-null 五类 span，安全构造。 */
function appendJsonHighlighted(pre, text) {
  const re = /("(?:\\.|[^"\\])*")(\s*:)?|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|\b(true|false|null)\b/g;
  let last = 0, m;
  const emit = (s, cls) => {
    if (!s) return;
    if (cls) {
      const sp = document.createElement('span');
      sp.className = cls;
      sp.textContent = s;
      pre.appendChild(sp);
    } else {
      pre.appendChild(document.createTextNode(s));
    }
  };
  while ((m = re.exec(text)) !== null) {
    emit(text.slice(last, m.index));
    if (m[1] !== undefined) {
      emit(m[1], m[2] ? 'j-key' : 'j-str');
      if (m[2]) emit(m[2]);
    } else if (m[3] !== undefined) {
      emit(m[3], 'j-num');
    } else {
      emit(m[4], 'j-bool');
    }
    last = re.lastIndex;
  }
  emit(text.slice(last));
}

/* ── 日志来源：SSE 优先，失败降级轮询 ─────────── */

function startPolling() {
  createPoller(async () => {
    const data = await API.logSince(seq);
    if (data.unauthorized || data.offline) { setOnline(false); return; }
    if (!Array.isArray(data)) { setOnline(false); return; }
    setOnline(true);
    data.forEach(appendEntry);
  }, 800).start();
}

function startLogSource() {
  if (typeof EventSource === 'undefined') {
    startPolling();
    return;
  }
  const es = new EventSource(API.logStreamUrl);

  // 服务端每 400ms 必发一条（无增量也发 ": ping" 心跳注释）。连接被静默掐断
  // （如游戏进程被强杀、网络骤断，收不到 TCP 断开事件）时靠超时判离线。
  let lastEvent = Date.now();
  const watchdog = setInterval(() => {
    if (es.readyState === EventSource.OPEN && Date.now() - lastEvent > 3000)
      setOnline(false);
  }, 2000);

  es.onopen = () => { lastEvent = Date.now(); setOnline(true); };
  es.onmessage = (ev) => {
    lastEvent = Date.now();
    try {
      const batch = JSON.parse(ev.data);
      if (Array.isArray(batch)) batch.forEach(appendEntry);
    } catch { /* 半包忽略，下一批自愈 */ }
    setOnline(true);
  };
  es.onerror = () => {
    // 游戏进程退出/断网时浏览器只会进入 CONNECTING 无限自动重连，CLOSED
    // （须降级轮询）并非必达——两种状态都置离线，重连成功由 onopen 恢复
    setOnline(false);
    if (es.readyState === EventSource.CLOSED) {
      clearInterval(watchdog);
      es.close();
      startPolling();
    }
  };
}

/* ── 命令补全 ──────────────────────────────── */

function getPartial() {
  const m = inp.value.match(/^([/!]?)(\S*)$/);
  if (!m) return null;
  return { prefix: m[1], partial: m[2].toLowerCase() };
}

function filterCommands() {
  const p = getPartial();
  if (p === null) { hideSuggest(); return; }
  matches = commands.filter(c => {
    if (!p.partial) return true;
    if ((c.name || '').toLowerCase().startsWith(p.partial)) return true;
    return (c.aliases || []).some(a => String(a).toLowerCase().startsWith(p.partial));
  });
  if (!matches.length) { hideSuggest(); return; }
  selIdx = 0;
  renderSuggest();
}

function renderSuggest() {
  const key = matches.map(c => c.name).join('\u0001');
  if (key !== renderedKey) {
    renderedKey = key;
    sugEl.innerHTML = '';
    sugEl.scrollTop = 0;
    matches.forEach((c, i) => {
      const d = document.createElement('div');
      d.className = 'sug-item';
      // name/description/author 来自命令注册表（静态），仍走转义保持纪律
      d.innerHTML =
        '<span class="sug-name">/' + esc(c.name) + '</span>' +
        '<span class="sug-desc">' + esc(c.description || c.usage || '') + '</span>' +
        (c.author ? '<span class="sug-author">功能制作者：' + esc(c.author) + '</span>' : '');
      d.onmousedown = (e) => { e.preventDefault(); applyMatch(i); };
      d.onmouseenter = () => { selIdx = i; updateSel(); };
      sugEl.appendChild(d);
    });
  }
  updateSel();
  sugEl.classList.add('open');
}

function updateSel() {
  const items = sugEl.children;
  for (let i = 0; i < items.length; i++)
    items[i].classList.toggle('sel', i === selIdx);
  scrollSelectedIntoView();
}

function scrollSelectedIntoView() {
  const el = sugEl.children[selIdx];
  if (!el) return;
  const top = el.offsetTop;
  const bottom = top + el.offsetHeight;
  if (top < sugEl.scrollTop) sugEl.scrollTop = top;
  else if (bottom > sugEl.scrollTop + sugEl.clientHeight)
    sugEl.scrollTop = bottom - sugEl.clientHeight;
}

function hideSuggest() {
  sugEl.classList.remove('open');
  matches = [];
  selIdx = -1;
  renderedKey = '';
}

function applyMatch(idx) {
  const c = matches[idx];
  if (!c) return;
  inp.value = '/' + c.name + ' ';
  hideSuggest();
  inp.focus();
}

/* ── 历史记录（localStorage 持久化） ─────────── */

function loadHistory() {
  try {
    const raw = localStorage.getItem(HISTORY_KEY);
    hist = raw ? JSON.parse(raw) : [];
  } catch { hist = []; }
  if (!Array.isArray(hist)) hist = [];
  hIdx = hist.length;
}

function pushHistory(v) {
  hist.push(v);
  if (hist.length > HISTORY_MAX) hist = hist.slice(-HISTORY_MAX);
  hIdx = hist.length;
  try { localStorage.setItem(HISTORY_KEY, JSON.stringify(hist)); } catch { /* 隐私模式忽略 */ }
}

/* ── 发送：消费 CommandResult 信封 ───────────── */

async function send() {
  const v = inp.value.trim();
  if (!v) return;
  pushHistory(v);
  appendLocal('> ' + v, 'var(--cyan)');
  inp.value = '';
  hideSuggest();
  const r = await API.run(v);
  if (r.unauthorized) return;
  appendResult(r);
}

/* ── Steam 在线人数（同源代理，60s 一轮） ────── */

function startSteamPoller() {
  const box = document.getElementById('steam-players');
  const count = document.getElementById('steam-players-count');
  createPoller(async () => {
    const d = await API.steamPlayers();
    if (!d.ok || typeof d.players !== 'number') {
      box && box.classList.add('fail');
      box && box.classList.remove('ok');
      return;
    }
    if (count) count.textContent = d.players.toLocaleString('en-US');
    box && box.classList.remove('fail');
    box && box.classList.add('ok');
  }, 60000).start();
}

/* ── 装配 ──────────────────────────────────── */

export function mountConsole() {
  logEl = document.getElementById('log');
  inp = document.getElementById('input');
  sugEl = document.getElementById('suggest');
  statusEl = document.getElementById('status');
  statusText = document.getElementById('status-text');

  loadHistory();
  startLogSource();
  startSteamPoller();

  (async () => {
    const list = await API.commands();
    commands = Array.isArray(list) ? list : [];
  })();

  // 失焦收起补全（候选项用 mousedown+preventDefault 避免抢焦点）
  inp.addEventListener('blur', hideSuggest);
  inp.addEventListener('input', filterCommands);
  inp.addEventListener('keydown', (e) => {
    if (sugEl.classList.contains('open') && matches.length) {
      if (e.key === 'ArrowDown') { e.preventDefault(); selIdx = (selIdx + 1) % matches.length; renderSuggest(); return; }
      if (e.key === 'ArrowUp') { e.preventDefault(); selIdx = (selIdx - 1 + matches.length) % matches.length; renderSuggest(); return; }
      if (e.key === 'Tab' || (e.key === 'Enter' && selIdx >= 0 && getPartial())) {
        e.preventDefault();
        applyMatch(selIdx);
        if (e.key === 'Enter') return;
        return;
      }
      if (e.key === 'Escape') { hideSuggest(); return; }
    }
    if (e.key === 'Enter') { e.preventDefault(); send(); return; }
    if (e.key === 'ArrowUp' && !sugEl.classList.contains('open')) {
      e.preventDefault();
      if (hist.length && hIdx > 0) { hIdx--; inp.value = hist[hIdx]; }
      return;
    }
    if (e.key === 'ArrowDown' && !sugEl.classList.contains('open')) {
      e.preventDefault();
      if (hIdx < hist.length - 1) { hIdx++; inp.value = hist[hIdx]; }
      else { hIdx = hist.length; inp.value = ''; }
    }
  });
  document.getElementById('send').onclick = send;
}
