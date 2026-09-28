<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { API } from '../../api.js';
import { useConfigToolbar } from '../../composables/useConfigToolbar.js';
import { useSectionMeta } from '../../composables/useSectionMeta.js';
import FolderGrid from '../common/FolderGrid.vue';
import EntryList from '../common/EntryList.vue';
import LogPanel from '../common/LogPanel.vue';
import { splitDesc } from '../config/configUtils.js';
import IconDeviceFloppy from '~icons/tabler/device-floppy';
import IconDownload from '~icons/tabler/download';
import IconUpload from '~icons/tabler/upload';
import IconRefresh from '~icons/tabler/refresh';
import IconTrash from '~icons/tabler/trash';
import IconHistory from '~icons/tabler/history';
import IconArrowLeft from '~icons/tabler/arrow-left';
import IconRotateClockwise2 from '~icons/tabler/rotate-clockwise-2';

const hostEnabled = ref(false);
const modules = ref([]);
const loadError = ref('');
const query = ref('');
const editingField = ref(false);
const openModuleId = ref(null);   // 展开中的模块 id，null=文件夹网格视图
const logOpen = ref(false);
const sectionMeta = useSectionMeta();

async function reload() {
  const data = await API.automationStatus();
  if (data.unauthorized) return;
  if (data.error && !data.modules) { loadError.value = data.error; return; }
  loadError.value = '';
  hostEnabled.value = !!data.hostEnabled;
  modules.value = data.modules || [];
  // 展开中的模块用最新数据顶替，自动刷新时字段值实时回写
  if (openModuleId.value) {
    const fresh = modules.value.find(m => m.id === openModuleId.value);
    if (fresh) currentModule.value = fresh;
  }
}

const { dirty, autoRefresh, toast, showToast, save, exportCfg, importFile, resetAll, startAutoRefresh, stopAutoRefresh }
  = useConfigToolbar({ flag: 'automation', reload, isEditing: () => editingField.value || !!openModuleId.value });

const currentModule = ref(null);
function openDetail(item) {
  const m = modules.value.find(x => x.id === item.key);
  if (!m) return;
  currentModule.value = m;
  openModuleId.value = m.id;
}
function backToGrid() {
  openModuleId.value = null;
  currentModule.value = null;
  logOpen.value = false;
}

function matchesQuery(m, q) {
  if ((m.displayName || '').toLowerCase().includes(q)) return true;
  if ((sectionMeta.displayName(m.id) || '').toLowerCase().includes(q)) return true;
  if ((m.id || '').toLowerCase().includes(q)) return true;
  if ((m.description || '').toLowerCase().includes(q)) return true;
  return (m.entries || []).some(e =>
    (e.key || '').toLowerCase().includes(q) || (e.description || '').toLowerCase().includes(q));
}

const folderItems = computed(() =>
  modules.value
    .filter(m => {
      const q = query.value.trim().toLowerCase();
      return !q || matchesQuery(m, q);
    })
    .map(m => ({
      key: m.id,
      label: m.displayName || m.id,
      count: (m.entries || []).length,
      enabled: !!m.enabled,
    }))
);

async function toggleHost() {
  const next = !hostEnabled.value;
  const j = await API.automationHost(next);
  if (j.unauthorized) return;
  if (!j.ok) { showToast(j.error || '总开关切换失败', true); return; }
  hostEnabled.value = next;
  dirty.value = true;
  showToast(next ? '自动化总开关：开' : '自动化总开关：关');
}

async function commitEntry(m, entry, value) {
  const j = await API.configUpdate(m.section, entry.key, value);
  if (j.unauthorized) return;
  if (!j.ok) { showToast(j.error || '更新失败', true); return; }
  entry.value = j.value != null ? j.value : value;
  dirty.value = true;
  showToast(`${m.section}.${entry.key} 已更新`);
}

async function resetModule() {
  const m = currentModule.value;
  if (!m) return;
  if (!confirm(`恢复模块 [${m.section}] 全部默认值？（仅做临时调整，如需持久化请使用保存功能。）`)) return;
  const j = await API.configReset(m.section);
  if (j.unauthorized) return;
  if (j.ok) { dirty.value = true; showToast(`已重置 ${j.reset} 项`); }
  else showToast(j.error || '重置失败', true);
}

const modMeta = computed(() => {
  const m = currentModule.value;
  if (!m) return { author: '', side: '', desc: '' };
  const enabledEntry = (m.entries || []).find(e => e.key === 'Enabled');
  const parsed = enabledEntry ? splitDesc(enabledEntry.description) : { author: '', side: '', text: '' };
  return {
    author: m.author || parsed.author,
    side: m.side || parsed.side,
    desc: m.description || parsed.text || '',
  };
});

onMounted(async () => {
  await reload();
  startAutoRefresh();
});
onUnmounted(stopAutoRefresh);
</script>

<template>
  <div class="automation-app">
    <div class="toolbar">
      <div class="toolbar-left">
        <label class="host-switch">
          <input type="checkbox" :checked="hostEnabled" @change="toggleHost">
          <span class="switch-track"><span class="switch-thumb" /></span>
          <span class="host-label">自动化总开关</span>
        </label>
        <span class="host-hint">{{ hostEnabled ? '自动化宿主运行中' : '总开关关闭时所有模块待机' }}</span>
      </div>
      <div class="toolbar-right">
        <span v-if="dirty" class="dirty-mark">●未保存</span>
        <button class="tb-btn" :class="{ active: autoRefresh }" @click="autoRefresh = !autoRefresh">
          <IconRefresh /> 自动刷新:{{ autoRefresh ? '开' : '关' }}
        </button>
        <button class="tb-btn" @click="save"><IconDeviceFloppy /> 保存</button>
        <button class="tb-btn" @click="exportCfg"><IconDownload /> 导出</button>
        <button class="tb-btn" @click="importFile('memory')"><IconUpload /> 导入(内存)</button>
        <button class="tb-btn" @click="importFile('overwrite')"><IconUpload /> 导入(写盘)</button>
        <button class="tb-btn danger" @click="resetAll"><IconTrash /> 全部重置</button>
      </div>
    </div>
    <div class="search-row">
      <input v-if="!openModuleId" v-model="query" class="search" type="search" placeholder="搜索文件夹 / 字段…">
      <span v-else class="breadcrumb">
        全部模块 / <b :title="openModuleId">{{ sectionMeta.displayName(openModuleId) || (currentModule && (currentModule.displayName || currentModule.id)) || openModuleId }}</b>
      </span>
    </div>

    <div v-if="toast" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <div v-if="loadError" class="panel-empty">{{ loadError }}</div>

    <FolderGrid v-else-if="!openModuleId" :items="folderItems" @open="openDetail" />

    <div v-else-if="currentModule" class="module-detail">
      <div class="detail-header">
        <button class="back-btn" @click="backToGrid"><IconArrowLeft /> 全部模块</button>
        <div class="detail-title">
          <span class="bracket-name" :title="currentModule.id">[{{ sectionMeta.displayName(currentModule.id) || currentModule.displayName || currentModule.id }}]</span>
          <span v-if="modMeta.side" class="badge">{{ modMeta.side }}</span>
          <span v-if="modMeta.author" class="author">by {{ modMeta.author }}</span>
        </div>
        <div class="detail-actions">
          <button class="icon-btn" :class="{ active: logOpen }" title="模块日志" @click="logOpen = !logOpen"><IconHistory /></button>
          <button class="icon-btn" title="恢复模块默认" @click="resetModule"><IconRotateClockwise2 /></button>
        </div>
      </div>
      <p v-if="modMeta.desc" class="mod-desc">{{ modMeta.desc }}</p>
      <div v-if="logOpen" class="mod-log">
        <LogPanel
          :fetch="() => API.moduleLog(currentModule.id)"
          :clear="async () => { await API.moduleLogClear(currentModule.id); }"
        />
      </div>
      <div class="mod-fields">
        <EntryList :entries="currentModule.entries || []" @commit="(e, v) => commitEntry(currentModule, e, v)" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.automation-app { display: flex; flex-direction: column; height: 100%; background: var(--surface-0); position: relative; }

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 10px 14px 6px;
  flex-shrink: 0;
  flex-wrap: wrap;
  background: var(--surface-1);
}
.toolbar-left { display: flex; align-items: center; gap: 10px; }
.host-switch { display: flex; align-items: center; gap: 8px; cursor: pointer; }
.host-switch input { position: absolute; opacity: 0; width: 0; height: 0; }
.switch-track {
  width: 34px; height: 20px;
  background: var(--surface-2);
  border: 1px solid var(--line-strong);
  border-radius: 11px;
  position: relative;
}
.switch-thumb {
  position: absolute; top: 2px; left: 2px;
  width: 14px; height: 14px; border-radius: 50%;
  background: var(--text-2);
  transition: transform 0.15s var(--ease), background 0.15s var(--ease);
}
.host-switch input:checked + .switch-track { background: rgba(39, 127, 255, 0.18); border-color: var(--accent-cyan-dim); }
.host-switch input:checked + .switch-track .switch-thumb { transform: translateX(14px); background: var(--accent-cyan); }
.host-label { font-size: 12.5px; color: var(--text-0); font-weight: 600; }
.host-hint { font-size: 11.5px; color: var(--text-2); }

.toolbar-right { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.dirty-mark { font-size: 11px; color: var(--accent-amber); margin-right: 4px; }
.tb-btn {
  display: flex; align-items: center; gap: 5px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 11.5px;
  padding: 6px 10px;
  cursor: pointer;
  white-space: nowrap;
}
.tb-btn svg { width: 13px; height: 13px; }
.tb-btn:hover { color: var(--text-0); border-color: var(--line-strong); }
.tb-btn.active { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.tb-btn.danger:hover { color: var(--accent-red); border-color: var(--accent-red); }

.search-row { padding: 6px 14px 10px; background: var(--surface-1); border-bottom: 1px solid var(--line); }
.search {
  width: 100%; max-width: 260px;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12.5px;
  padding: 7px 10px;
  outline: none;
}
.breadcrumb { font-size: 12.5px; color: var(--text-1); }
.breadcrumb b { color: var(--accent-cyan); font-family: var(--font-mono); }

.toast {
  position: absolute; top: 100px; right: 16px;
  background: var(--surface-2);
  border: 1px solid var(--accent-cyan-dim);
  border-radius: var(--radius-sm);
  padding: 8px 14px;
  font-size: 12px;
  color: var(--text-0);
  z-index: 100;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}
.toast.error { border-color: var(--accent-red); color: var(--accent-red); }

.panel-empty { text-align: center; color: var(--text-2); padding: 40px 0; font-size: 12.5px; }

.module-detail { flex: 1; display: flex; flex-direction: column; min-height: 0; }
.detail-header {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--line);
  flex-shrink: 0;
  flex-wrap: wrap;
}
.back-btn {
  display: flex; align-items: center; gap: 6px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 6px 11px;
  cursor: pointer;
  flex-shrink: 0;
}
.back-btn:hover { color: var(--text-0); }
.back-btn svg { width: 13px; height: 13px; }
.detail-title { display: flex; align-items: baseline; gap: 8px; flex: 1; min-width: 0; }
.bracket-name { font-family: var(--font-mono); font-size: 14px; font-weight: 600; color: var(--accent-cyan); }
.badge { font-size: 10.5px; color: var(--text-2); border: 1px solid var(--line-strong); border-radius: 4px; padding: 1px 6px; }
.author { font-size: 11.5px; color: var(--text-2); }
.detail-actions { display: flex; align-items: center; gap: 6px; }
.icon-btn {
  width: 30px; height: 30px;
  display: grid; place-items: center;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.icon-btn:hover, .icon-btn.active { color: var(--text-0); border-color: var(--line-strong); }
.icon-btn.active { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.icon-btn svg { width: 14px; height: 14px; }

.mod-desc { font-size: 11.5px; color: var(--text-2); margin: 10px 16px 0; line-height: 1.5; flex-shrink: 0; }
.mod-log { margin: 10px 16px 0; flex-shrink: 0; }
.mod-fields { flex: 1; overflow-y: auto; min-height: 0; }
</style>
