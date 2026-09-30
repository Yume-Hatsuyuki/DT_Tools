<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { API } from '../../api.js';
import { useConfigToolbar } from '../../composables/useConfigToolbar.js';
import { useSectionMeta } from '../../composables/useSectionMeta.js';
import FolderGrid from '../common/FolderGrid.vue';
import SectionDetail from './SectionDetail.vue';
import { sectionMatches } from './configUtils.js';
import IconDeviceFloppy from '~icons/tabler/device-floppy';
import IconDownload from '~icons/tabler/download';
import IconUpload from '~icons/tabler/upload';
import IconRefresh from '~icons/tabler/refresh';
import IconTrash from '~icons/tabler/trash';

const sections = ref([]);
const query = ref('');
const openSection = ref(null);   // 展开中的段名，null=文件夹网格视图
const sectionMeta = useSectionMeta();

async function reload() {
  const list = await API.configList();
  if (list.unauthorized) return;
  sections.value = Array.isArray(list) ? list : [];
  if (openSection.value) {
    const fresh = sections.value.find(s => s.section === openSection.value);
    if (fresh) currentSection.value = fresh;
  }
}

const { dirty, autoRefresh, toast, showToast, save, exportCfg, importFile, resetAll, startAutoRefresh, stopAutoRefresh }
  = useConfigToolbar({ flag: 'config', reload, isEditing: () => !!openSection.value });

const currentSection = ref(null);
/** FolderGrid 抛出的是磁贴摘要 {key,label,count,enabled}，需按 key 找回完整段对象。 */
function openDetail(item) {
  const sec = sections.value.find(s => s.section === item.key);
  if (!sec) return;
  currentSection.value = sec;
  openSection.value = sec.section;
}
function backToGrid() {
  openSection.value = null;
  currentSection.value = null;
}

function onUpdated() { dirty.value = true; }
/** 段级重置：除标脏外立即 reload——否则界面停在旧值，与"已重置"的提示相反。 */
async function onReset() { dirty.value = true; await reload(); }
function onToast(t) { showToast(t.text, t.error); }

/**
 * 目录职责分离：本页只显示功能段（group=feature）。
 * 自动化段（总开关 + 各模块，group=automation）归"自动化"应用展示——
 * 分组字段由后端 ConfigService.ClassifyGroup 给出，前端不硬编码段名。
 */
const featureSections = computed(() => sections.value.filter(s => s.group !== 'automation'));

const folderItems = computed(() =>
  featureSections.value
    .filter(s => {
      const q = query.value.trim().toLowerCase();
      if (!q) return true;
      const alias = (sectionMeta.displayName(s.section) || '').toLowerCase();
      return alias.includes(q) || sectionMatches(s, q);
    })
    .map(s => ({
      key: s.section,
      label: s.section,
      count: (s.entries || []).length,
      enabled: enabledOf(s),
    }))
);

function enabledOf(section) {
  // 开关键名走协议字段（后端 Engine.EnabledKey），不硬编码 'Enabled'
  const key = section.enabledKey || 'Enabled';
  const e = (section.entries || []).find(en => en.key === key);
  return e ? !!e.value : null;
}

onMounted(async () => {
  await reload();
  startAutoRefresh();
});
onUnmounted(stopAutoRefresh);
</script>

<template>
  <div class="config-app">
    <div class="toolbar">
      <div class="toolbar-left">
        <input v-if="!openSection" v-model="query" class="search" type="search" placeholder="搜索文件夹 / 字段…">
        <span v-else class="breadcrumb">
          全部配置 / <b :title="openSection">{{ sectionMeta.displayName(openSection) || openSection }}</b>
        </span>
      </div>
      <div class="toolbar-right">
        <span v-if="dirty" class="dirty-mark" title="有未保存的更改">●未保存</span>
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

    <div v-if="toast" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <FolderGrid v-if="!openSection" :items="folderItems" @open="openDetail" />
    <SectionDetail
      v-else-if="currentSection"
      :section="currentSection"
      @back="backToGrid"
      @updated="onUpdated"
      @reset="onReset"
      @toast="onToast"
    />
  </div>
</template>

<style scoped>
.config-app { display: flex; flex-direction: column; height: 100%; background: var(--surface-0); }

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 10px 14px;
  border-bottom: 1px solid var(--line);
  flex-shrink: 0;
  flex-wrap: wrap;
  background: var(--surface-1);
}
.toolbar-left { flex: 1; min-width: 160px; }
.search {
  width: 100%;
  max-width: 260px;
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

.toast {
  position: absolute;
  top: 54px;
  right: 16px;
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
</style>
