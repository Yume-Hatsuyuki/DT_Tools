/**
 * 配置视图：功能与基础设施配置（group !== "automation" 的段）。
 * 段落分流完全依据后端的 group 字段，前端不认识任何具体段名。
 */

import { API } from '../api.js';
import { toast, splitDesc, buildSecHead, buildModLog, buildEntryRow, buildEntryControl } from '../ui.js';
import { wireToolbar } from './toolbar.js';

let listEl, searchEl;
let sections = [];
let filter = '';
const cfgLogState = new Map();
let toolbar;

function isEditingInPanel() {
  const ae = document.activeElement;
  if (!ae || !listEl.contains(ae)) return false;
  const tag = (ae.tagName || '').toLowerCase();
  return tag === 'input' || tag === 'select' || tag === 'textarea';
}

async function load() {
  const data = await API.configList();
  if (!Array.isArray(data)) return;
  sections = data.filter(sec => sec.group !== 'automation');
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

    // Author/Side 元数据写在 Enabled 项的描述注释里
    let author = '', side = '';
    for (const e of (sec.entries || [])) {
      if (e.key === 'Enabled') {
        const meta = splitDesc(e.description);
        author = meta.author;
        side = meta.side;
        break;
      }
    }

    const { head } = buildSecHead({ name: sec.section, side, author, bracketName: true });

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

    card.appendChild(buildModLog({
      key: sec.section,
      stateMap: cfgLogState,
      fetchLog: () => API.sectionLog(sec.section),
      clearLog: () => API.sectionLogClear(sec.section),
    }));

    listEl.appendChild(card);
  });

  if (!listEl.childElementCount)
    listEl.innerHTML = '<div class="panel-empty">无匹配配置。</div>';
}

function row(section, e) {
  const { text } = splitDesc(e.description);
  return buildEntryRow({
    key: e.key,
    type: e.type,
    description: text,
    control: buildEntryControl(e, (v) => update(section, e.key, v)),
  });
}

async function update(section, key, value) {
  const j = await API.configUpdate(section, key, value);
  if (j.unauthorized) return;
  if (!j.ok) { toast(j.error || '更新失败', true); return; }
  toolbar.setDirty(true);
  toast(section + '.' + key + ' 已更新');
  // 就地回写成功值，自动刷新前也显示新值
  const sec = sections.find(s => s.section === section);
  const entry = sec && (sec.entries || []).find(e => e.key === key);
  if (entry) entry.value = j.value != null ? j.value : value;
}

async function resetSection(section) {
  if (!confirm('恢复段 [' + section + '] 全部默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
  const j = await API.configReset(section);
  if (j.unauthorized) return;
  if (j.ok) { toolbar.setDirty(true); toast('已重置 ' + j.reset + ' 项'); await load(); }
  else toast(j.error || '重置失败', true);
}

export function mountConfig() {
  listEl = document.getElementById('cfg-list');
  searchEl = document.getElementById('cfg-search');

  toolbar = wireToolbar({
    flag: 'config',
    dirtyEl: document.getElementById('cfg-dirty'),
    autoBtn: document.getElementById('cfg-autorefresh'),
    saveBtn: document.getElementById('cfg-save'),
    exportBtn: document.getElementById('cfg-export-cfg'),
    importMemBtn: document.getElementById('cfg-import-mem'),
    importOverBtn: document.getElementById('cfg-import-over'),
    resetAllBtn: document.getElementById('cfg-reset-all'),
    isEditingInPanel,
    reload: load,
  });

  searchEl.addEventListener('input', () => { filter = searchEl.value; render(); });

  load();
}

export function setActiveConfig(on) {
  if (toolbar) toolbar.setActive(on);
  if (on) load();
}
