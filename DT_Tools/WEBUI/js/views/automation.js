/**
 * 自动化视图：总开关 + 模块卡片（配置项 + 模块日志）。
 * 数据源 /api/automation/status；配置写回统一走 /api/config/update。
 */

import { API } from '../api.js';
import { toast, esc, splitDesc, buildSecHead, buildModLog, buildEntryRow, buildEntryControl } from '../ui.js';
import { wireToolbar } from './toolbar.js';

let listEl, hostCb, hintEl, searchEl;
let modulesCache = [];
let autoFilter = '';
let toolbar;
const logState = new Map();

function isEditingInPanel() {
  const ae = document.activeElement;
  if (!ae || !listEl.contains(ae)) return false;
  const tag = (ae.tagName || '').toLowerCase();
  return tag === 'input' || tag === 'select' || tag === 'textarea';
}

async function load() {
  const data = await API.automationStatus();
  if (data.unauthorized) return;
  if (data.error && !data.modules) {
    listEl.innerHTML = '<div class="panel-empty">' + esc(data.error) + '</div>';
    return;
  }
  if (hostCb) {
    hostCb.checked = !!data.hostEnabled;
    hintEl.textContent = data.hostEnabled ? '自动化宿主运行中' : '总开关关闭时所有模块待机';
  }
  modulesCache = data.modules || [];
  renderList();
}

function renderList() {
  listEl.innerHTML = '';
  if (!modulesCache.length) {
    listEl.innerHTML = '<div class="panel-empty">暂无已注册的自动化模块。</div>';
    return;
  }

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
  if (!mods.length) {
    listEl.innerHTML = '<div class="panel-empty">无匹配模块。</div>';
    return;
  }
  mods.forEach(m => listEl.appendChild(renderModule(m)));
}

function renderModule(m) {
  const card = document.createElement('div');
  card.className = 'sec-card';

  const { head } = buildSecHead({
    name: m.displayName || m.id,
    side: m.side,
    author: m.author,
    bracketName: false,
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
    // Enabled 项描述里带 Author/Side 元数据，剥掉只留正文（复用 ui.js 的 splitDesc，与 config 页一致）
    const text = e.key === 'Enabled' ? splitDesc(e.description).text : (e.description || '');
    body.appendChild(buildEntryRow({
      key: e.key,
      type: e.type,
      description: text,
      control: buildEntryControl(e, (v) => update(m.section, e.key, v)),
    }));
  });
  card.appendChild(body);

  card.appendChild(buildModLog({
    key: m.id,
    stateMap: logState,
    fetchLog: () => API.moduleLog(m.id),
    clearLog: () => API.moduleLogClear(m.id),
  }));

  return card;
}

async function update(section, key, value) {
  const j = await API.configUpdate(section, key, value);
  if (j.unauthorized) return;
  if (!j.ok) { toast(j.error || '更新失败', true); return; }
  toolbar.setDirty(true);
  toast(section + '.' + key + ' 已更新');
}

export function mountAutomation() {
  listEl = document.getElementById('auto-list');
  hostCb = document.getElementById('auto-host-enabled');
  hintEl = document.getElementById('auto-host-hint');
  searchEl = document.getElementById('auto-search');

  toolbar = wireToolbar({
    flag: 'automation',
    dirtyEl: document.getElementById('auto-dirty'),
    autoBtn: document.getElementById('auto-autorefresh'),
    saveBtn: document.getElementById('auto-save'),
    exportBtn: document.getElementById('auto-export-cfg'),
    importMemBtn: document.getElementById('auto-import-mem'),
    importOverBtn: document.getElementById('auto-import-over'),
    resetAllBtn: document.getElementById('auto-reset-all'),
    isEditingInPanel,
    reload: load,
  });

  if (hostCb) {
    hostCb.addEventListener('change', async () => {
      const j = await API.automationHost(hostCb.checked);
      if (j.unauthorized) return;
      if (!j.ok) {
        toast(j.error || '总开关切换失败', true);
        hostCb.checked = !hostCb.checked;
        return;
      }
      toolbar.setDirty(true);
      toast(hostCb.checked ? '自动化总开关：开' : '自动化总开关：关');
      hintEl.textContent = hostCb.checked ? '自动化宿主运行中' : '总开关关闭时所有模块待机';
    });
  }

  if (searchEl) {
    searchEl.addEventListener('input', () => { autoFilter = searchEl.value; renderList(); });
  }

  load();
}

export function setActiveAutomation(on) {
  if (toolbar) toolbar.setActive(on);
  if (on) load();
}
