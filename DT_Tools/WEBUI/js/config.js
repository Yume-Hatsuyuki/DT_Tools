(function () {
  const U = window.DTUI;
  const listEl = document.getElementById('cfg-list');
  const dirtyEl = document.getElementById('cfg-dirty');
  const searchEl = document.getElementById('cfg-search');
  let sections = [];
  let dirty = false;
  let filter = '';
  const cfgLogState = new Map();

  function isAutomationSection(name) {
    const s = String(name || '');
    return s === 'Automation' || s.startsWith('Auto.');
  }

  function setDirty(v) {
    dirty = v;
    if (dirtyEl) dirtyEl.style.display = v ? 'inline' : 'none';
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
      if (isAutomationSection(sec.section)) return;
      if (q && !sec.section.toLowerCase().includes(q) &&
          !(sec.entries || []).some(e =>
            (e.key || '').toLowerCase().includes(q) ||
            (e.description || '').toLowerCase().includes(q)))
        return;

      const card = document.createElement('div');
      card.className = 'sec-card';

      let author = '', side = '';
      for (const e of (sec.entries || [])) {
        if (e.key === 'Enabled') {
          const meta = U.splitDesc(e.description);
          author = meta.author;
          side = meta.side;
          break;
        }
      }

      const { head } = U.buildSecHead({
        name: sec.section,
        side,
        author,
        bracketName: true
      });

      const resetBtn = document.createElement('button');
      resetBtn.className = 'btn secondary';
      resetBtn.textContent = '恢复本段默认';
      resetBtn.onclick = () => resetSection(sec.section);
      head.appendChild(resetBtn);
      card.appendChild(head);

      const body = document.createElement('div');
      body.className = 'sec-body';
      (sec.entries || []).forEach(e => body.appendChild(row(sec.section, e)));
      card.appendChild(body);

      card.appendChild(U.buildModLog({
        key: sec.section,
        stateMap: cfgLogState,
        fetchLog: async () => {
          const r = await fetch('/api/config/section/' + encodeURIComponent(sec.section) + '/log');
          return r.json();
        },
        clearLog: async () => {
          await fetch('/api/config/section/' + encodeURIComponent(sec.section) + '/log/clear', { method: 'POST' });
        }
      }));

      listEl.appendChild(card);
    });
  }

  function row(section, e) {
    const { text } = U.splitDesc(e.description);
    return U.buildEntryRow({
      key: e.key,
      type: e.type,
      description: text,
      control: control(section, e)
    });
  }

  function control(section, e) {
    const ctl = U.buildEntryControl(e, (v) => update(section, e.key, v, e));
    if (section === 'CustomRoomName' && e.key === 'RoomName') {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.gap = '8px';
      row.style.alignItems = 'center';
      row.style.flexWrap = 'wrap';
      row.style.marginTop = '6px';
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'btn secondary';
      btn.textContent = '应用到当前房间';
      btn.title = '将 RoomName 写入 Steam Lobby（需房主且已在房间内）';
      btn.onclick = () => {
        let v = e.value;
        const input = ctl.querySelector('input, textarea');
        if (input) v = input.value;
        update(section, e.key, v, e);
      };
      row.appendChild(btn);
      ctl.appendChild(row);
    }
    return ctl;
  }

  async function update(section, key, value, entry) {
    const r = await fetch('/api/config/update', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ section, key, value })
    });
    const j = await r.json();
    if (!j.ok) { U.toast(j.error || '更新失败', true); return; }
    setDirty(true);
    U.toast(section + '.' + key + ' 已更新');
  }

  async function save() {
    const r = await fetch('/api/config/save', { method: 'POST' });
    const j = await r.json();
    if (j.ok) { setDirty(false); U.toast('已保存到 .cfg'); }
    else U.toast(j.error || '保存失败', true);
  }

  async function resetSection(section) {
    if (!confirm('恢复段 [' + section + '] 全部默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
    const r = await fetch('/api/config/reset', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ section })
    });
    const j = await r.json();
    if (j.ok) { setDirty(true); U.toast('已重置 ' + j.reset + ' 项'); await load(); }
  }

  async function resetAll() {
    if (!confirm('恢复全部配置默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
    const r = await fetch('/api/config/reset', { method: 'POST', body: '{}' });
    const j = await r.json();
    if (j.ok) { setDirty(true); U.toast('已重置 ' + j.reset + ' 项'); await load(); }
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
      if (!j.ok) { U.toast(j.error || '导入失败', true); return; }
      if (mode === 'memory') setDirty(true);
      else setDirty(false);
      U.toast('导入完成：updated=' + j.updated + ' skipped=' + j.skipped +
        (j.errors && j.errors.length ? ' errors=' + j.errors.length : ''));
      await load();
    };
    input.click();
  }

  document.getElementById('cfg-save').onclick = save;
  document.getElementById('cfg-export-cfg').onclick = () => download('/api/config/export.cfg', 'DT_Tools.cfg');
  document.getElementById('cfg-import-mem').onclick = () => importFile('memory');
  document.getElementById('cfg-import-over').onclick = () => importFile('overwrite');
  document.getElementById('cfg-reset-all').onclick = resetAll;
  searchEl.addEventListener('input', () => { filter = searchEl.value; render(); });

  let autoRefresh = true;
  let panelActive = false;
  let panelTimer = null;
  const autoBtn = document.getElementById('cfg-autorefresh');

  function syncAutoBtn() {
    if (!autoBtn) return;
    autoBtn.textContent = autoRefresh ? '自动刷新:开' : '自动刷新:关';
    autoBtn.title = autoRefresh ? '点击关闭面板自动刷新' : '点击开启面板自动刷新';
  }
  syncAutoBtn();

  function isEditingInPanel() {
    const ae = document.activeElement;
    if (!ae || !listEl.contains(ae)) return false;
    const tag = (ae.tagName || '').toLowerCase();
    return tag === 'input' || tag === 'select' || tag === 'textarea';
  }

  function stopPanelPoll() {
    if (panelTimer) {
      clearInterval(panelTimer);
      panelTimer = null;
    }
  }

  function startPanelPoll() {
    stopPanelPoll();
    if (!panelActive || !autoRefresh) return;
    panelTimer = setInterval(() => {
      if (!panelActive || !autoRefresh) return;
      if (isEditingInPanel()) return;
      load();
    }, 3000);
  }

  if (autoBtn) {
    autoBtn.onclick = () => {
      autoRefresh = !autoRefresh;
      syncAutoBtn();
      if (autoRefresh) {
        load();
        startPanelPoll();
      } else stopPanelPoll();
    };
  }

  window.DTConfig = {
    load,
    setDirty,
    setActive: (on) => {
      panelActive = !!on;
      if (panelActive) {
        load();
        startPanelPoll();
      } else stopPanelPoll();
    }
  };
})();
