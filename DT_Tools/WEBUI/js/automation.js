(function () {
  const U = window.DTUI;
  const listEl = document.getElementById('auto-list');
  const hostCb = document.getElementById('auto-host-enabled');
  const hintEl = document.getElementById('auto-host-hint');
  const dirtyEl = document.getElementById('auto-dirty');
  let dirty = false;
  const logState = new Map();
  let modulesCache = [];
  let autoFilter = '';

  function setDirty(v) {
    dirty = !!v;
    if (dirtyEl) dirtyEl.style.display = dirty ? 'inline' : 'none';
  }

  async function api(path, opts) {
    const r = await fetch(path, opts);
    const t = await r.text();
    try { return JSON.parse(t); } catch { return { ok: false, error: t }; }
  }

  function controlForEntry(mod, e) {
    return U.buildEntryControl(e, (v) => update(mod.section, e.key, v));
  }

  function renderModule(m) {
    const card = document.createElement('div');
    card.className = 'sec-card';
    card.dataset.id = m.id;
    card.dataset.section = m.section;

    const { head } = U.buildSecHead({
      name: m.displayName || m.id,
      side: m.side,
      author: m.author,
      bracketName: false
    });
    card.appendChild(head);

    const body = document.createElement('div');
    body.className = 'sec-body';

    if (m.description) {
      const desc = document.createElement('div');
      desc.className = 'entry-desc module-lead';
      desc.textContent = m.description;
      body.appendChild(desc);
    }

    (m.entries || []).forEach(e => {
      const { text } = U.splitDesc(e.description);
      body.appendChild(U.buildEntryRow({
        key: e.key,
        type: e.type,
        description: text || e.description,
        control: controlForEntry(m, e)
      }));
    });
    card.appendChild(body);

    card.appendChild(U.buildModLog({
      key: m.id,
      stateMap: logState,
      fetchLog: () => api('/api/automation/modules/' + encodeURIComponent(m.id) + '/log'),
      clearLog: () => api('/api/automation/modules/' + encodeURIComponent(m.id) + '/log/clear', { method: 'POST' })
    }));

    return card;
  }

  async function update(section, key, value) {
    const res = await api('/api/config/update', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ section, key, value: String(value) })
    });
    if (!res.ok) { U.toast(res.error || '更新失败', true); return; }
    setDirty(true);
    U.toast(section + '.' + key + ' 已更新');
    await load();
  }

  async function load() {
    const data = await api('/api/automation/status');
    if (data.error && !data.modules) {
      listEl.innerHTML = '<div class="panel-empty">' + U.esc(data.error) + '</div>';
      return;
    }
    hostCb.checked = !!data.hostEnabled;
    hintEl.textContent = data.hostEnabled ? '自动化宿主运行中' : '总开关关闭时所有模块待机';
    modulesCache = data.modules || [];
    renderList();
  }

  function renderList() {
    listEl.innerHTML = '';
    const q = autoFilter.trim().toLowerCase();
    const mods = modulesCache.filter(m => {
      if (!q) return true;
      if ((m.displayName || '').toLowerCase().includes(q)) return true;
      if ((m.id || '').toLowerCase().includes(q)) return true;
      if ((m.section || '').toLowerCase().includes(q)) return true;
      if ((m.description || '').toLowerCase().includes(q)) return true;
      return (m.entries || []).some(e =>
        (e.key || '').toLowerCase().includes(q) ||
        (e.description || '').toLowerCase().includes(q));
    });
    if (!modulesCache.length) {
      listEl.innerHTML = '<div class="panel-empty">暂无已注册的自动化模块。</div>';
      return;
    }
    if (!mods.length) {
      listEl.innerHTML = '<div class="panel-empty">无匹配模块。</div>';
      return;
    }
    mods.forEach(m => listEl.appendChild(renderModule(m)));
  }

  hostCb.addEventListener('change', async () => {
    const res = await api('/api/automation/host', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled: hostCb.checked })
    });
    if (!res.ok) {
      U.toast(res.error || '总开关切换失败', true);
      hostCb.checked = !hostCb.checked;
      return;
    }
    setDirty(true);
    U.toast(hostCb.checked ? '自动化总开关：开' : '自动化总开关：关');
    hintEl.textContent = hostCb.checked ? '自动化宿主运行中' : '总开关关闭时所有模块待机';
  });

  document.getElementById('auto-save').onclick = async () => {
    const res = await api('/api/config/save', { method: 'POST' });
    if (res.ok) {
      setDirty(false);
      U.toast('已保存到 .cfg');
      if (window.DTConfig && window.DTConfig.setDirty) window.DTConfig.setDirty(false);
    } else U.toast(res.error || '保存失败', true);
  };
  document.getElementById('auto-export-cfg').onclick = () => {
    const a = document.createElement('a');
    a.href = '/api/config/export.cfg';
    a.download = 'DT_Tools.cfg';
    a.click();
  };

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
      const res = await api('/api/config/import', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ format, mode, content })
      });
      if (!res.ok) { U.toast(res.error || '导入失败', true); return; }
      if (mode === 'memory') setDirty(true);
      else setDirty(false);
      U.toast('导入完成：updated=' + res.updated + ' skipped=' + res.skipped);
      await load();
    };
    input.click();
  }

  document.getElementById('auto-import-mem').onclick = () => importFile('memory');
  document.getElementById('auto-import-over').onclick = () => importFile('overwrite');
  document.getElementById('auto-reset-all').onclick = async () => {
    if (!confirm('恢复全部配置默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
    const res = await api('/api/config/reset', { method: 'POST', body: '{}' });
    if (res.ok) {
      setDirty(true);
      U.toast('已重置 ' + res.reset + ' 项');
      await load();
      if (window.DTConfig) window.DTConfig.load();
    } else U.toast(res.error || '重置失败', true);
  };

  const searchEl = document.getElementById('auto-search');
  if (searchEl) {
    searchEl.addEventListener('input', () => {
      autoFilter = searchEl.value;
      renderList();
    });
  }

  let autoRefresh = true;
  let panelActive = false;
  let panelTimer = null;
  const autoBtn = document.getElementById('auto-autorefresh');

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

  window.DTAutomation = {
    load,
    setActive: (on) => {
      panelActive = !!on;
      if (panelActive) {
        load();
        startPanelPoll();
      } else stopPanelPoll();
    }
  };
})();
