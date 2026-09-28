<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { API } from '../../api.js';
import { useConfigToolbar } from '../../composables/useConfigToolbar.js';
import { splitDesc } from '../config/configUtils.js';
import EntryControl from '../config/EntryControl.vue';
import IconDeviceFloppy from '~icons/tabler/device-floppy';
import IconDownload from '~icons/tabler/download';
import IconUpload from '~icons/tabler/upload';
import IconRefresh from '~icons/tabler/refresh';
import IconTrash from '~icons/tabler/trash';
import IconHistory from '~icons/tabler/history';

const hostEnabled = ref(false);
const modules = ref([]);
const loadError = ref('');
const query = ref('');
const editingField = ref(false);
const openLogs = ref({});   // moduleId -> log text | null

async function reload() {
  const data = await API.automationStatus();
  if (data.unauthorized) return;
  if (data.error && !data.modules) { loadError.value = data.error; return; }
  loadError.value = '';
  hostEnabled.value = !!data.hostEnabled;
  modules.value = data.modules || [];
}

const { dirty, autoRefresh, toast, showToast, save, exportCfg, importFile, resetAll, startAutoRefresh, stopAutoRefresh }
  = useConfigToolbar({ flag: 'automation', reload, isEditing: () => editingField.value });

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase();
  if (!q) return modules.value;
  return modules.value.filter(m => {
    if ((m.displayName || '').toLowerCase().includes(q)) return true;
    if ((m.id || '').toLowerCase().includes(q)) return true;
    if ((m.description || '').toLowerCase().includes(q)) return true;
    return (m.entries || []).some(e =>
      (e.key || '').toLowerCase().includes(q) || (e.description || '').toLowerCase().includes(q));
  });
});

function entryText(e) {
  return e.key === 'Enabled' ? splitDesc(e.description).text : (e.description || '');
}

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

async function toggleLog(m) {
  if (openLogs.value[m.id] != null) {
    const next = { ...openLogs.value };
    delete next[m.id];
    openLogs.value = next;
    return;
  }
  const r = await API.moduleLog(m.id);
  openLogs.value = {
    ...openLogs.value,
    [m.id]: Array.isArray(r) ? r.map(l => l.time + '  ' + l.msg).join('\n') : (r.error || '无日志'),
  };
}
async function clearLog(m) {
  await API.moduleLogClear(m.id);
  openLogs.value = { ...openLogs.value, [m.id]: '' };
}

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
      <input v-model="query" class="search" type="search" placeholder="搜索模块…">
    </div>

    <div v-if="toast" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <div class="module-list">
      <div v-if="loadError" class="panel-empty">{{ loadError }}</div>
      <div v-else-if="!filtered.length" class="panel-empty">
        {{ modules.length ? '无匹配模块。' : '暂无已注册的自动化模块。' }}
      </div>
      <div v-for="m in filtered" :key="m.id" class="mod-card">
        <div class="mod-head">
          <span class="mod-name">{{ m.displayName || m.id }}</span>
          <span v-if="m.side" class="badge">{{ m.side }}</span>
          <span v-if="m.author" class="author">by {{ m.author }}</span>
          <button class="icon-btn" title="模块日志" @click="toggleLog(m)"><IconHistory /></button>
        </div>
        <p v-if="m.description" class="mod-desc">{{ m.description }}</p>

        <div v-if="openLogs[m.id] != null" class="mod-log">
          <pre>{{ openLogs[m.id] || '（无日志）' }}</pre>
          <button class="btn-xs" @click="clearLog(m)">清空日志</button>
        </div>

        <div v-for="e in m.entries" :key="e.key" class="field-row">
          <div class="field-meta">
            <span class="field-key">{{ e.key }}</span>
            <span v-if="entryText(e)" class="field-desc">{{ entryText(e) }}</span>
          </div>
          <div class="field-control">
            <EntryControl :entry="e" @commit="v => commitEntry(m, e, v)" />
          </div>
        </div>
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
.host-switch input:checked + .switch-track { background: rgba(0, 229, 255, 0.18); border-color: var(--accent-cyan-dim); }
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

.module-list { flex: 1; overflow-y: auto; padding: 12px 14px 24px; display: flex; flex-direction: column; gap: 10px; }
.panel-empty { text-align: center; color: var(--text-2); padding: 40px 0; font-size: 12.5px; }

.mod-card {
  background: var(--surface-1);
  border: 1px solid var(--line);
  border-radius: var(--radius-md);
  padding: 12px 14px;
}
.mod-head { display: flex; align-items: center; gap: 8px; }
.mod-name { font-size: 13px; font-weight: 600; color: var(--text-0); }
.badge { font-size: 10.5px; color: var(--text-2); border: 1px solid var(--line-strong); border-radius: 4px; padding: 1px 6px; }
.author { font-size: 11px; color: var(--text-2); }
.icon-btn {
  margin-left: auto;
  width: 26px; height: 26px;
  display: grid; place-items: center;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.icon-btn:hover { color: var(--text-0); }
.icon-btn svg { width: 13px; height: 13px; }
.mod-desc { font-size: 11.5px; color: var(--text-2); margin: 6px 0 4px; line-height: 1.5; }

.mod-log { margin: 8px 0; padding: 8px 10px; background: var(--surface-0); border-radius: var(--radius-sm); border: 1px solid var(--line); }
.mod-log pre { font-family: var(--font-mono); font-size: 11px; color: var(--text-1); max-height: 120px; overflow-y: auto; white-space: pre-wrap; margin-bottom: 6px; }
.btn-xs { font-size: 11px; padding: 3px 9px; background: var(--surface-2); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-1); cursor: pointer; }

.field-row { display: flex; align-items: center; gap: 16px; padding: 9px 0; border-top: 1px solid var(--line); }
.field-meta { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
.field-key { font-family: var(--font-mono); font-size: 12px; color: var(--text-0); }
.field-desc { font-size: 11px; color: var(--text-2); line-height: 1.5; }
.field-control { flex-shrink: 0; min-width: 160px; display: flex; justify-content: flex-end; }
</style>
