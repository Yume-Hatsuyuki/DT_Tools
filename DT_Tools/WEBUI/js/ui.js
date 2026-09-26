/**
 * 共用 UI 组件：转义、Toast、卡片头、配置行、控件工厂、可折叠模块日志。
 * 所有动态文本一律 textContent 或 esc()，禁止裸 innerHTML 拼接游戏文本。
 * 控件按后端元数据（type / accepts）驱动，不认识任何具体 key。
 */

import { createPoller } from './api.js';

export function esc(s) {
  return String(s ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function toast(msg, err) {
  const el = document.getElementById('toast');
  if (!el) return;
  el.textContent = msg;
  el.className = 'show' + (err ? ' err' : '');
  clearTimeout(toast._t);
  toast._t = setTimeout(() => { el.className = ''; }, 2800);
}

export function formatSide(side) {
  const k = String(side || '').toLowerCase();
  if (k === 'client' || side === '客户端') return '客户端';
  if (k === 'host' || side === '服务端') return '服务端';
  if (k === 'both' || side === '双方' || side === '双端') return '双端';
  return side || '';
}

/** 解析 BepInEx 描述里的 Author / Side 行（引擎写进 Enabled 项注释的元数据）。 */
export function splitDesc(desc) {
  const lines = String(desc || '').split(/\r?\n/);
  let author = '', side = '';
  const body = [];
  for (const line of lines) {
    const a = line.match(/^Author:\s*(.*)$/i);
    if (a) { author = a[1].trim(); continue; }
    const s = line.match(/^Side:\s*(.*)$/i);
    if (s) { side = s[1].trim(); continue; }
    body.push(line);
  }
  while (body.length && !body[0].trim()) body.shift();
  while (body.length && !body[body.length - 1].trim()) body.pop();
  return { author, side, text: body.join('\n') };
}

/** 卡片头：[名称] + 作用范围 + 制作者。 */
export function buildSecHead(opt) {
  const head = document.createElement('div');
  head.className = 'sec-head';
  const title = document.createElement('div');
  title.className = 'sec-title';
  const name = document.createElement('span');
  name.className = 'sec-name';
  name.textContent = opt.bracketName ? '[' + opt.name + ']' : opt.name;
  title.appendChild(name);
  if (opt.side) {
    const sd = document.createElement('span');
    sd.className = 'sec-side';
    sd.textContent = formatSide(opt.side);
    title.appendChild(sd);
  }
  if (opt.author) {
    const au = document.createElement('span');
    au.className = 'sec-author';
    au.textContent = '功能制作者：' + opt.author;
    title.appendChild(au);
  }
  head.appendChild(title);
  return { head, title };
}

/**
 * 可折叠模块日志：展开后自动刷新（seq 未变时跳过重绘），
 * 可关自动刷新，可清空。标签页隐藏时轮询自动暂停。
 * @param {{ key, fetchLog: ()=>Promise<{ok,seq?,lines?,error?}>, clearLog: ()=>Promise, stateMap: Map }} opt
 */
export function buildModLog(opt) {
  const stateMap = opt.stateMap;
  if (!stateMap.has(opt.key)) stateMap.set(opt.key, { open: false, auto: true, lastSeq: -1 });
  const st = stateMap.get(opt.key);
  if (typeof st.auto !== 'boolean') st.auto = true;

  const details = document.createElement('details');
  details.className = 'mod-log';
  details.open = !!st.open;

  const summary = document.createElement('summary');
  summary.textContent = '模块日志';
  details.appendChild(summary);

  const tools = document.createElement('div');
  tools.className = 'mod-log-tools';
  const btnAuto = document.createElement('button');
  btnAuto.type = 'button';
  btnAuto.className = 'btn secondary btn-xs';
  const btnClear = document.createElement('button');
  btnClear.type = 'button';
  btnClear.className = 'btn secondary btn-xs';
  btnClear.textContent = '清空';
  tools.appendChild(btnAuto);
  tools.appendChild(btnClear);
  details.appendChild(tools);

  const pre = document.createElement('pre');
  pre.className = 'mod-log-body';
  pre.textContent = '加载中…';
  details.appendChild(pre);

  const syncAutoLabel = () => {
    btnAuto.textContent = st.auto ? '自动刷新:开' : '自动刷新:关';
    btnAuto.title = st.auto ? '点击关闭自动刷新' : '点击开启自动刷新';
  };
  syncAutoLabel();

  async function pull() {
    try {
      const j = await opt.fetchLog();
      if (!j || !j.ok) {
        pre.textContent = (j && j.error) || '加载失败';
        return;
      }
      if (typeof j.seq === 'number' && j.seq === st.lastSeq && pre.textContent !== '加载中…')
        return; // seq 未变：跳过重绘
      st.lastSeq = typeof j.seq === 'number' ? j.seq : st.lastSeq;
      const next = (j.lines && j.lines.length) ? j.lines.join('\n') : '（空）';
      const stick = pre.scrollHeight - pre.scrollTop - pre.clientHeight < 24;
      pre.textContent = next;
      if (stick) pre.scrollTop = pre.scrollHeight;
    } catch (e) {
      pre.textContent = String(e);
    }
  }

  const poll = createPoller(pull, 2000);
  const startPoll = () => { pull(); poll.start(); };
  const stopPoll = () => poll.stop();

  details.addEventListener('toggle', () => {
    st.open = details.open;
    if (details.open && st.auto) startPoll();
    else stopPoll();
  });
  btnAuto.onclick = (ev) => {
    ev.preventDefault();
    st.auto = !st.auto;
    syncAutoLabel();
    if (!details.open) return;
    stopPoll();
    if (st.auto) startPoll();
    else pull();
  };
  btnClear.onclick = async (ev) => {
    ev.preventDefault();
    try { await opt.clearLog(); } catch { /* 清空失败下次刷新可见 */ }
    st.lastSeq = -1;
    pull();
  };

  if (st.open) setTimeout(startPoll, 0);

  return details;
}

/** 参数行：左 key/type，右控件，下描述。 */
export function buildEntryRow(opt) {
  const row = document.createElement('div');
  row.className = 'entry-row';
  const head = document.createElement('div');
  head.className = 'entry-head';
  const left = document.createElement('div');
  left.className = 'entry-left';
  const key = document.createElement('div');
  key.className = 'entry-key';
  key.textContent = opt.key || '';
  left.appendChild(key);
  if (opt.type) {
    const ty = document.createElement('div');
    ty.className = 'entry-meta';
    ty.textContent = opt.type;
    left.appendChild(ty);
  }
  const right = document.createElement('div');
  right.className = 'entry-ctrl';
  if (opt.control) right.appendChild(opt.control);
  head.appendChild(left);
  head.appendChild(right);
  row.appendChild(head);
  if (opt.description) {
    const d = document.createElement('div');
    d.className = 'entry-desc';
    d.textContent = opt.description;
    row.appendChild(d);
  }
  return row;
}

/** 布尔开关。 */
export function buildBoolControl(checked, onChange) {
  const wrap = document.createElement('div');
  wrap.className = 'bool-wrap';
  const lab = document.createElement('label');
  lab.className = 'bool' + (checked ? ' on' : '');
  const cb = document.createElement('input');
  cb.type = 'checkbox';
  cb.checked = !!checked;
  const span = document.createElement('span');
  span.className = 'bool-text';
  span.textContent = cb.checked ? 'true' : 'false';
  cb.addEventListener('change', () => {
    span.textContent = cb.checked ? 'true' : 'false';
    lab.classList.toggle('on', cb.checked);
    if (onChange) onChange(cb.checked);
  });
  lab.appendChild(cb);
  lab.appendChild(span);
  wrap.appendChild(lab);
  return wrap;
}

/**
 * 解析后端 accepts → 统一选项数组 [{value,label}]，无选项返回 null。
 * 后端契约：accepts = { options:[{value,label}] }（下拉/枚举/动态提供者）或 { min, max }（数值范围）或 null。
 */
export function normalizeOptions(accepts) {
  if (!accepts || typeof accepts !== 'object') return null;
  const opts = accepts.options;
  if (!Array.isArray(opts) || !opts.length) return null;
  return opts.map(o => ({
    value: String(o.value),
    label: o.label != null && o.label !== '' ? String(o.label) : String(o.value),
  }));
}

/** 下拉框。当前值不在选项内时补一项，避免 .cfg 里手写的值被 UI 悄悄吞掉。 */
export function buildSelect(options, current, onChange) {
  const sel = document.createElement('select');
  sel.className = 'cfg-select';
  const cur = current == null ? '' : String(current);

  options.forEach(opt => {
    const o = document.createElement('option');
    o.value = opt.value;
    o.textContent = opt.label;
    if (opt.value === cur) o.selected = true;
    sel.appendChild(o);
  });

  if (cur && !options.some(opt => opt.value === cur)) {
    const o = document.createElement('option');
    o.value = cur;
    o.textContent = cur + ' (未在列表中)';
    o.selected = true;
    sel.appendChild(o);
  }

  sel.addEventListener('change', () => onChange(sel.value));
  return sel;
}

/**
 * 文本 / 数字输入框（失焦或回车提交）。
 * accepts 为 {min,max} 时写入浏览器约束并在提交前校验，越界不提交、toast 提示。
 */
export function buildInput(type, current, accepts, onCommit) {
  const t = (type || '').toLowerCase();
  const isNumber = t.includes('int') || t.includes('single') || t.includes('double') || t.includes('float');
  const input = document.createElement('input');
  input.className = 'cfg-input';
  input.type = isNumber ? 'number' : 'text';
  input.value = current != null ? String(current) : '';

  let min = null, max = null;
  if (isNumber && accepts && typeof accepts === 'object') {
    if (typeof accepts.min === 'number') { min = accepts.min; input.min = String(min); }
    if (typeof accepts.max === 'number') { max = accepts.max; input.max = String(max); }
  }

  const commit = () => {
    const v = input.value;
    if (isNumber && v !== '') {
      const n = Number(v);
      if ((min != null && n < min) || (max != null && n > max)) {
        toast('超出范围 ' + (min ?? '-∞') + ' ~ ' + (max ?? '+∞'), true);
        input.value = current != null ? String(current) : '';
        return;
      }
    }
    onCommit(v);
  };
  input.addEventListener('change', commit);
  input.addEventListener('keydown', ev => { if (ev.key === 'Enter') commit(); });
  return input;
}

/**
 * 按配置项自身的元数据选择控件——CONFIG / AUTOMATION 两页共用：
 *   bool → 开关；有 options → 下拉；其它 → 输入框（数值带范围校验）。
 * @param {{ key, type, value, accepts }} entry 后端 EntryDto
 * @param {(newValue) => void} onCommit
 */
export function buildEntryControl(entry, onCommit) {
  const type = (entry.type || '').toLowerCase();
  if (type === 'boolean')
    return buildBoolControl(!!entry.value, onCommit);

  const options = normalizeOptions(entry.accepts);
  if (options)
    return buildSelect(options, entry.value, onCommit);

  return buildInput(entry.type, entry.value, entry.accepts, onCommit);
}
