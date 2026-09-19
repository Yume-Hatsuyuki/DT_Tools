/**
 * DT Tools 共用 UI 模板：转义、Toast、Side 文案、模块卡片、可折叠日志。
 * CONFIG / AUTOMATION 共用，保证字体与结构一致。
 */
(function (global) {
  const toastEl = () => document.getElementById('toast');

  function esc(s) {
    return String(s ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function toast(msg, err) {
    const el = toastEl();
    if (!el) return;
    el.textContent = msg;
    el.className = 'show' + (err ? ' err' : '');
    clearTimeout(toast._t);
    toast._t = setTimeout(() => { el.className = ''; }, 2800);
  }

  function formatSide(side) {
    const k = String(side || '').toLowerCase();
    if (k === 'client' || side === '客户端') return '客户端';
    if (k === 'host' || side === '服务端') return '服务端';
    if (k === 'both' || side === '双方' || side === '双端') return '双端';
    return side || '';
  }

  /**
   * 解析 BepInEx 描述里的 Author / Side 行。
   */
  function splitDesc(desc) {
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

  /**
   * 模块卡片头：名称 + 作用范围 + 制作者。
   * @param {{ name:string, side?:string, author?:string, bracketName?:boolean }} opt
   */
  function buildSecHead(opt) {
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
   * 可折叠模块日志。
   * @param {{ key:string, fetchLog:()=>Promise<{ok:boolean,lines?:string[],error?:string}>, clearLog:()=>Promise<void>, stateMap:Map }} opt
   */
  function buildModLog(opt) {
    const key = opt.key;
    const stateMap = opt.stateMap;
    if (!stateMap.has(key)) stateMap.set(key, { open: false, timer: null });
    const st = stateMap.get(key);

    const details = document.createElement('details');
    details.className = 'mod-log';
    details.open = !!st.open;

    const summary = document.createElement('summary');
    summary.textContent = '模块日志';
    details.appendChild(summary);

    const tools = document.createElement('div');
    tools.className = 'mod-log-tools';
    const btnR = document.createElement('button');
    btnR.type = 'button';
    btnR.className = 'btn secondary btn-xs';
    btnR.textContent = '刷新';
    const btnC = document.createElement('button');
    btnC.type = 'button';
    btnC.className = 'btn secondary btn-xs';
    btnC.textContent = '清空';
    tools.appendChild(btnR);
    tools.appendChild(btnC);
    details.appendChild(tools);

    const pre = document.createElement('pre');
    pre.className = 'mod-log-body';
    pre.textContent = '加载中…';
    details.appendChild(pre);

    async function pull() {
      try {
        const j = await opt.fetchLog();
        if (!j || !j.ok) {
          pre.textContent = (j && j.error) || '加载失败';
          return;
        }
        pre.textContent = (j.lines && j.lines.length) ? j.lines.join('\n') : '（空）';
        pre.scrollTop = pre.scrollHeight;
      } catch (e) {
        pre.textContent = String(e);
      }
    }

    function startPoll() {
      pull();
      if (!st.timer) st.timer = setInterval(pull, 2500);
    }
    function stopPoll() {
      if (st.timer) {
        clearInterval(st.timer);
        st.timer = null;
      }
    }

    details.addEventListener('toggle', () => {
      st.open = details.open;
      if (details.open) startPoll();
      else stopPoll();
    });
    btnR.onclick = (ev) => { ev.preventDefault(); pull(); };
    btnC.onclick = async (ev) => {
      ev.preventDefault();
      try { await opt.clearLog(); } catch (_) {}
      pull();
    };

    if (st.open) setTimeout(startPoll, 0);

    return details;
  }

  /**
   * 参数行：左 key/type，右控件，下描述。
   */
  function buildEntryRow(opt) {
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

  /** 布尔开关（CONFIG / AUTOMATION 同款） */
  function buildBoolControl(checked, onChange) {
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
   * 规范化后端 accepts → 统一的选项数组 [{value,label}]，无选项则返回 null。
   * 后端协议：accepts = { options:[{value,label}], values:[string] } 或 { min, max } 或 null。
   * 兼容：老版本直接返回字符串数组 / {values:[...]}（无 label，此时 label=value）。
   */
  function normalizeOptions(accepts) {
    if (!accepts) return null;
    if (Array.isArray(accepts))
      return accepts.length ? accepts.map(v => ({ value: String(v), label: String(v) })) : null;

    const opts = accepts.options || accepts.Options;
    if (Array.isArray(opts) && opts.length)
      return opts.map(o => ({
        value: String(o.value),
        label: o.label != null && o.label !== '' ? String(o.label) : String(o.value)
      }));

    const vals = accepts.values || accepts.Values;
    if (Array.isArray(vals) && vals.length)
      return vals.map(v => ({ value: String(v), label: String(v) }));

    return null;
  }

  /** 下拉框。当前值不在选项内时补一项，避免 .cfg 里手写的值被 UI 悄悄吞掉。 */
  function buildSelect(options, current, onChange) {
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

  /** 文本 / 数字输入框（失焦或回车提交）。 */
  function buildInput(type, current, onCommit) {
    const t = (type || '').toLowerCase();
    const input = document.createElement('input');
    input.className = 'cfg-input';
    input.type = (t.includes('int') || t.includes('single') || t.includes('double') || t.includes('float'))
      ? 'number' : 'text';
    input.value = current != null ? String(current) : '';
    const commit = () => onCommit(input.value);
    input.addEventListener('change', commit);
    input.addEventListener('keydown', ev => { if (ev.key === 'Enter') commit(); });
    return input;
  }

  /**
   * 按配置项自身的元数据选择控件——CONFIG / AUTOMATION 两页共用，不认任何具体 key：
   *   bool            → 开关
   *   有 options/values → 下拉（含动态提供者、枚举、AcceptableValueList）
   *   其它             → 输入框
   * @param entry    后端 EntryDto：{ key, type, value, accepts }
   * @param onCommit (newValue) => void
   */
  function buildEntryControl(entry, onCommit) {
    const type = (entry.type || '').toLowerCase();
    if (type === 'boolean')
      return buildBoolControl(!!entry.value, onCommit);

    const options = normalizeOptions(entry.accepts !== undefined ? entry.accepts : entry.Accepts);
    if (options)
      return buildSelect(options, entry.value, onCommit);

    return buildInput(entry.type, entry.value, onCommit);
  }

  global.DTUI = {
    esc,
    toast,
    formatSide,
    splitDesc,
    buildSecHead,
    buildModLog,
    buildEntryRow,
    buildBoolControl,
    normalizeOptions,
    buildSelect,
    buildInput,
    buildEntryControl
  };
})(window);
