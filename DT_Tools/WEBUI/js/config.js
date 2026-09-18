(function () {
  const listEl = document.getElementById('cfg-list');
  const dirtyEl = document.getElementById('cfg-dirty');
  const searchEl = document.getElementById('cfg-search');
  const toastEl = document.getElementById('toast');
  let sections = [];
  let dirty = false;
  let filter = '';

  function toast(msg, err) {
    toastEl.textContent = msg;
    toastEl.className = 'show' + (err ? ' err' : '');
    setTimeout(() => { toastEl.className = ''; }, 2800);
  }

  function setDirty(v) {
    dirty = v;
    dirtyEl.style.display = v ? 'inline' : 'none';
  }

  async function load() {
    const r = await fetch('/api/config/list');
    sections = await r.json();
    render();
  }

  function render() {
    const q = filter.trim().toLowerCase();
    listEl.innerHTML = '';
    sections.forEach(sec => {
      if (q && !sec.section.toLowerCase().includes(q) &&
          !(sec.entries || []).some(e =>
            (e.key || '').toLowerCase().includes(q) ||
            (e.description || '').toLowerCase().includes(q)))
        return;

      const card = document.createElement('div');
      card.className = 'sec-card';
      const head = document.createElement('div');
      head.className = 'sec-head';

      const title = document.createElement('div');
      title.className = 'sec-title';
      title.innerHTML = '<span class="sec-name">[' + esc(sec.section) + ']</span>';

      // 元数据取自 Enabled 描述中的 Author / Side 行
      let author = '', side = '';
      for (const e of (sec.entries || [])) {
        if (e.key === 'Enabled') {
          const meta = splitDesc(e.description);
          author = meta.author;
          side = meta.side;
          break;
        }
      }
      if (side) {
        const sd = document.createElement('span');
        sd.className = 'sec-side';
        sd.textContent = formatSide(side);
        title.appendChild(sd);
      }
      if (author) {
        const au = document.createElement('span');
        au.className = 'sec-author';
        au.textContent = '功能制作者：' + author;
        title.appendChild(au);
      }

      const resetBtn = document.createElement('button');
      resetBtn.className = 'btn secondary';
      resetBtn.textContent = '恢复本段默认';
      resetBtn.onclick = () => resetSection(sec.section);

      head.appendChild(title);
      head.appendChild(resetBtn);
      card.appendChild(head);

      const body = document.createElement('div');
      body.className = 'sec-body';
      (sec.entries || []).forEach(e => body.appendChild(row(sec.section, e)));
      card.appendChild(body);
      listEl.appendChild(card);
    });
  }

  function splitDesc(desc) {
    const lines = String(desc || '').split(/\r?\n/);
    let author = '';
    let side = '';
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

  function formatSide(side) {
    const k = String(side || '').toLowerCase();
    if (k === 'client') return '客户端';
    if (k === 'host') return '服务端';
    if (k === 'both') return '双端';
    return side || '';
  }

  function row(section, e) {
    const div = document.createElement('div');
    div.className = 'entry-row';
    const { text } = splitDesc(e.description);

    const head = document.createElement('div');
    head.className = 'entry-head';

    const left = document.createElement('div');
    left.className = 'entry-left';
    const meta = esc(e.type || '');
    left.innerHTML =
      '<div class="entry-key">' + esc(e.key) + '</div>' +
      (meta ? '<div class="entry-meta">' + meta + '</div>' : '');

    const right = document.createElement('div');
    right.className = 'entry-ctrl';
    right.appendChild(control(section, e));

    head.appendChild(left);
    head.appendChild(right);
    div.appendChild(head);

    if (text) {
      const desc = document.createElement('div');
      desc.className = 'entry-desc';
      desc.textContent = text; // 保留换行；textContent 自动转义
      div.appendChild(desc);
    }
    return div;
  }

  function control(section, e) {
    const type = (e.type || '').toLowerCase();
    if (type === 'boolean') {
      const wrap = document.createElement('div');
      wrap.className = 'bool-wrap';

      const lab = document.createElement('label');
      lab.className = 'bool';
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.checked = !!e.value;
      const span = document.createElement('span');
      span.className = 'bool-text';
      span.textContent = cb.checked ? 'true' : 'false';
      cb.addEventListener('change', () => {
        span.textContent = cb.checked ? 'true' : 'false';
        lab.classList.toggle('on', cb.checked);
        update(section, e.key, cb.checked, e);
      });
      lab.classList.toggle('on', cb.checked);
      lab.appendChild(cb);
      lab.appendChild(span);
      wrap.appendChild(lab);

      if (e.key === 'Enabled') {
        const tip = document.createElement('div');
        tip.className = 'bool-tip';
        tip.textContent = '改 Enabled 后需保存并重启才能装卸补丁';
        wrap.appendChild(tip);
      }
      return wrap;
    }

    // 枚举 / AcceptableValueList → 下拉（互斥选项）
    // API 可能返回 string[] 或 { values: [...] } / { Values: [...] }
    let accepts = e.accepts || e.Accepts;
    if (accepts && !Array.isArray(accepts))
      accepts = accepts.values || accepts.Values || null;
    if (Array.isArray(accepts) && accepts.length > 0) {
      const sel = document.createElement('select');
      sel.className = 'cfg-select';
      const cur = e.value == null ? '' : String(e.value);
      accepts.forEach(opt => {
        const o = document.createElement('option');
        o.value = String(opt);
        o.textContent = String(opt);
        if (String(opt) === cur) o.selected = true;
        sel.appendChild(o);
      });
      if (!accepts.map(String).includes(cur) && cur) {
        const o = document.createElement('option');
        o.value = cur;
        o.textContent = cur + ' (当前)';
        o.selected = true;
        sel.appendChild(o);
      }
      sel.addEventListener('change', () => update(section, e.key, sel.value, e));
      return sel;
    }

    const input = document.createElement('input');
    input.type = (type === 'int32' || type === 'single' || type === 'double') ? 'number' : 'text';
    if (type === 'single' || type === 'double') input.step = 'any';
    input.value = e.value == null ? '' : e.value;
    const commit = () => update(section, e.key, input.value, e);
    input.addEventListener('change', commit);
    input.addEventListener('blur', commit);
    return input;
  }

  async function update(section, key, value, entry) {
    try {
      const r = await fetch('/api/config/update', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ section, key, value })
      });
      const j = await r.json();
      if (!j.ok) {
        toast(j.error || '更新失败', true);
        return;
      }
      if (entry) entry.value = j.value;
      setDirty(true);
    } catch (err) {
      toast(String(err), true);
    }
  }

  async function save() {
    const r = await fetch('/api/config/save', { method: 'POST', body: '{}' });
    const j = await r.json();
    if (j.ok) { setDirty(false); toast('已保存到 .cfg'); }
    else toast(j.error || '保存失败', true);
  }

  async function resetSection(section) {
    if (!confirm('恢复段 [' + section + '] 全部默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
    const r = await fetch('/api/config/reset', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ section })
    });
    const j = await r.json();
    if (j.ok) { setDirty(true); toast('已重置 ' + j.reset + ' 项'); await load(); }
  }

  async function resetAll() {
    if (!confirm('恢复全部配置默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
    const r = await fetch('/api/config/reset', { method: 'POST', body: '{}' });
    const j = await r.json();
    if (j.ok) { setDirty(true); toast('已重置 ' + j.reset + ' 项'); await load(); }
  }

  function download(path, filename) {
    const a = document.createElement('a');
    a.href = path;
    a.download = filename;
    a.click();
  }

  function importFile(mode) {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.cfg,.json,text/plain,application/json';
    input.onchange = async () => {
      const file = input.files[0];
      if (!file) return;
      if (mode === 'overwrite' && !confirm('覆盖配置将写入磁盘 .cfg，确定？')) return;
      const content = await file.text();
      const format = file.name.toLowerCase().endsWith('.cfg') ? 'cfg' : 'json';
      const r = await fetch('/api/config/import', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ format, mode, content })
      });
      const j = await r.json();
      if (!j.ok) { toast(j.error || '导入失败', true); return; }
      if (mode === 'memory') setDirty(true);
      else setDirty(false);
      toast('导入完成：updated=' + j.updated + ' skipped=' + j.skipped +
        (j.errors && j.errors.length ? ' errors=' + j.errors.length : ''));
      await load();
    };
    input.click();
  }

  function esc(s) {
    return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
  }

  document.getElementById('cfg-save').onclick = save;
  document.getElementById('cfg-export-cfg').onclick = () => download('/api/config/export.cfg', 'DT_Tools.cfg');
  document.getElementById('cfg-import-mem').onclick = () => importFile('memory');
  document.getElementById('cfg-import-over').onclick = () => importFile('overwrite');
  document.getElementById('cfg-reset-all').onclick = resetAll;
  searchEl.addEventListener('input', () => { filter = searchEl.value; render(); });

  window.DTConfig = { load, setActive: (on) => { if (on) load(); } };
})();
