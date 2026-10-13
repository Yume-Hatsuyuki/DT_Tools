<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { API } from '../../api.js';
import { useConfigToolbar } from '../../composables/useConfigToolbar.js';
import { useSectionMeta } from '../../composables/useSectionMeta.js';
import { useFolderView } from '../../composables/useFolderView.js';
import FolderGrid from '../common/FolderGrid.vue';
import SectionDetail from './SectionDetail.vue';
import { sectionMatches, splitDesc } from './configUtils.js';
import IconDeviceFloppy from '~icons/tabler/device-floppy';
import IconDownload from '~icons/tabler/download';
import IconUpload from '~icons/tabler/upload';
import IconRefresh from '~icons/tabler/refresh';
import IconTrash from '~icons/tabler/trash';
import IconLayoutGrid from '~icons/tabler/layout-grid';
import IconList from '~icons/tabler/list';

/**
 * 功能标签字典（分组 / 键 / 中文名 / 说明）由后端 FeatureTagCatalog 给出（/api/config/tags）。
 * 前端不写死任何标签：新增标签只改后端。
 */
const tagCatalog = ref([]);
/** 选中的标签键。空 = 不筛选；多选为「任一命中」。 */
const activeTags = ref([]);

const sections = ref([]);
const query = ref('');
const openSection = ref(null);   // 展开中的段名，null = 段列表
const currentSection = ref(null);
const sectionMeta = useSectionMeta();

// 网格/列表视图状态与切换：与自动化页**共用一份**（切换按钮在各自页面的工具栏上）
const { viewMode, toggleView } = useFolderView();

const tagByKey = computed(() => new Map(tagCatalog.value.map(t => [t.key, t])));
function tagLabel(key) { return tagByKey.value.get(key)?.label || key; }

/** 标签按领域分组，保持字典顺序。 */
const tagGroups = computed(() => {
  const groups = new Map();
  for (const t of tagCatalog.value) {
    if (!groups.has(t.group)) groups.set(t.group, []);
    groups.get(t.group).push(t);
  }
  return [...groups.entries()].map(([name, tags]) => ({ name, tags }));
});

/** 功能段（不含自动化段）。 */
const featureSections = computed(() => sections.value.filter(s => s.group !== 'automation'));

/** 每个标签下有多少功能段（芯片上的计数）。 */
const tagCounts = computed(() => {
  const counts = new Map();
  for (const s of featureSections.value) {
    for (const t of s.tags || []) counts.set(t, (counts.get(t) || 0) + 1);
  }
  return counts;
});

function toggleTag(key) {
  const i = activeTags.value.indexOf(key);
  if (i === -1) activeTags.value.push(key);
  else activeTags.value.splice(i, 1);
}
function clearFilters() { activeTags.value = []; statusFilter.value = null; sideFilter.value = null; }

/** 启用状态过滤：null = 不筛选，'on' = 仅启用，'off' = 仅未启用。与标签过滤为「且」关系。 */
const statusFilter = ref(null);
/** 启用 / 未启用的段数（只统计带开关的功能段；无开关的段两者都不计）。 */
const statusCounts = computed(() => {
  let on = 0, off = 0;
  for (const s of featureSections.value) {
    const e = enabledOf(s);
    if (e === true) on++;
    else if (e === false) off++;
  }
  return { on, off };
});
function toggleStatus(v) { statusFilter.value = statusFilter.value === v ? null : v; }
function matchStatus(s) {
  if (!statusFilter.value) return true;
  const e = enabledOf(s);
  return statusFilter.value === 'on' ? e === true : e === false;
}

/** 作用面过滤：null = 不筛选；'client' = 客户端，'host' = 服务端，'both' = 双端。与其它过滤为「且」关系。 */
const sideFilter = ref(null);
/** 各作用面的功能段数（无作用面的段不计）。 */
const sideCounts = computed(() => {
  const counts = { client: 0, host: 0, both: 0 };
  for (const s of featureSections.value) {
    if (s.side === 'client' || s.side === 'host' || s.side === 'both') counts[s.side]++;
  }
  return counts;
});
function toggleSide(v) { sideFilter.value = sideFilter.value === v ? null : v; }
function matchSide(s) { return !sideFilter.value || s.side === sideFilter.value; }

async function loadTags() {
  const cat = await API.configTags();
  if (cat.unauthorized) return;
  tagCatalog.value = Array.isArray(cat) ? cat : [];
}

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

/** FolderGrid 抛出的是磁贴摘要 {key,label,count,enabled}，需按 key 找回完整段对象。 */
function openDetail(item) {
  const sec = sections.value.find(s => s.section === item.key);
  if (!sec) return;
  currentSection.value = sec;
  openSection.value = sec.section;
}
function closeSection() {
  openSection.value = null;
  currentSection.value = null;
}

function onUpdated() { dirty.value = true; }
/** 段级重置：除标脏外立即 reload——否则界面停在旧值，与"已重置"的提示相反。 */
async function onReset() { dirty.value = true; await reload(); }
function onToast(t) { showToast(t.text, t.error); }

/** 段磁贴摘要（key=段名，FolderGrid 经 useSectionMeta 显示别名/图标）。 */
function toSectionTile(s) {
  return {
    key: s.section,
    label: s.section,
    count: (s.entries || []).length,
    enabled: enabledOf(s),
  };
}

/** 段有任一选中标签即命中；无选中标签则不筛选。 */
function matchTags(s) {
  if (!activeTags.value.length) return true;
  return (s.tags || []).some(t => activeTags.value.includes(t));
}

/** 搜索匹配：别名、标签中文名、段内字段。 */
function matchSection(s, q) {
  if (!q) return true;
  const alias = (sectionMeta.displayName(s.section) || '').toLowerCase();
  const tagText = (s.tags || []).map(tagLabel).join(' ').toLowerCase();
  return alias.includes(q) || tagText.includes(q) || sectionMatches(s, q);
}

const visibleSections = computed(() => {
  const q = query.value.trim().toLowerCase();
  return featureSections.value
    .filter(s => matchStatus(s) && matchSide(s) && matchTags(s) && matchSection(s, q))
    .map(toSectionTile);
});

function enabledOf(section) {
  // 开关键名走协议字段（后端 Engine.EnabledKey），不硬编码 'Enabled'
  const key = section.enabledKey || 'Enabled';
  const e = (section.entries || []).find(en => en.key === key);
  return e ? !!e.value : null;
}

/**
 * 列表行的右侧说明：段说明取自该段开关键（enabledKey）的描述，
 * 并用 splitDesc 剥掉「Author: … / Side: …」元数据行。
 * 行首附上功能标签，便于一眼看出功能所属领域。
 */
function descOf(item) {
  const s = sections.value.find(x => x.section === item.key);
  if (!s) return '';
  const key = s.enabledKey || 'Enabled';
  const e = (s.entries || []).find(en => en.key === key);
  const text = splitDesc(e && e.description).text;
  const first = text.split('\n').map(t => t.trim()).filter(Boolean)[0] || '';
  const tags = (s.tags || []).map(tagLabel);
  return tags.length ? `【${tags.join(' · ')}】${first}` : first;
}

onMounted(async () => {
  await loadTags();
  await reload();
  startAutoRefresh();
});
onUnmounted(stopAutoRefresh);
</script>

<template>
  <div class="config-app">
    <div class="toolbar">
      <div class="toolbar-left">
        <template v-if="openSection">
          <span class="breadcrumb">
            <a class="crumb" title="返回全部配置" @click="closeSection">全部配置</a>
            <span class="sep">/</span>
            <b :title="openSection">{{ sectionMeta.displayName(openSection) || openSection }}</b>
          </span>
        </template>
        <input v-else v-model="query" class="search" type="search" placeholder="搜索功能 / 标签 / 字段…">
      </div>
      <div class="toolbar-right">
        <span v-if="dirty" class="dirty-mark" title="有未保存的更改">●未保存</span>
        <button class="tb-btn"
                :title="viewMode === 'list' ? '切换到磁贴网格' : '切换到列表（每条带说明）'"
                @click="toggleView">
          <IconList v-if="viewMode === 'grid'" />
          <IconLayoutGrid v-else />
          {{ viewMode === 'grid' ? '列表' : '网格' }}
        </button>
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

    <div v-if="!openSection && tagGroups.length" class="tag-bar">
      <div class="tag-group">
        <span class="tag-group-name">状态</span>
        <button class="tag-chip" :class="{ on: statusFilter === 'on' }" @click="toggleStatus('on')">
          启用<em>{{ statusCounts.on }}</em>
        </button>
        <button class="tag-chip" :class="{ on: statusFilter === 'off' }" @click="toggleStatus('off')">
          未启用<em>{{ statusCounts.off }}</em>
        </button>
      </div>
      <div class="tag-group">
        <span class="tag-group-name">作用面</span>
        <button class="tag-chip" :class="{ on: sideFilter === 'client' }" @click="toggleSide('client')">
          客户端<em>{{ sideCounts.client }}</em>
        </button>
        <button class="tag-chip" :class="{ on: sideFilter === 'host' }" @click="toggleSide('host')">
          服务端<em>{{ sideCounts.host }}</em>
        </button>
        <button class="tag-chip" :class="{ on: sideFilter === 'both' }" @click="toggleSide('both')">
          双端<em>{{ sideCounts.both }}</em>
        </button>
      </div>
      <div v-for="g in tagGroups" :key="g.name" class="tag-group">
        <span class="tag-group-name">{{ g.name }}</span>
        <button v-for="t in g.tags" :key="t.key"
                class="tag-chip"
                :class="{ on: activeTags.includes(t.key) }"
                :title="t.label + '：' + t.description"
                @click="toggleTag(t.key)">
          {{ t.label }}<em v-if="tagCounts.get(t.key)">{{ tagCounts.get(t.key) }}</em>
        </button>
      </div>
      <div class="tag-tools">
        <span v-if="activeTags.length > 1" class="tag-hint">多选：任一命中</span>
        <button v-if="activeTags.length || statusFilter || sideFilter" class="tag-clear" @click="clearFilters">清除筛选</button>
      </div>
    </div>

    <div v-if="toast" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <FolderGrid v-if="!openSection" :items="visibleSections" :desc-of="descOf" @open="openDetail" />
    <SectionDetail
      v-if="currentSection"
      :section="currentSection"
      back-label="全部配置"
      @back="closeSection"
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
.toolbar-left { flex: 1; min-width: 160px; display: flex; align-items: center; gap: 10px; }
.search {
  flex: 1;
  min-width: 120px;
  max-width: 260px;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12.5px;
  padding: 7px 10px;
  outline: none;
}
.breadcrumb { font-size: 12.5px; color: var(--text-1); flex-shrink: 0; }
.breadcrumb b { color: var(--accent-cyan); font-family: var(--font-mono); }
.crumb { color: var(--text-1); cursor: pointer; }
.crumb:hover { color: var(--accent-cyan); text-decoration: underline; }
.sep { color: var(--text-2); margin: 0 3px; }

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

.tag-bar {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 6px;
  padding: 8px 14px;
  border-bottom: 1px solid var(--line);
  background: var(--surface-1);
  flex-shrink: 0;
}
.tag-group { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; }
.tag-group-name { font-size: 11px; color: var(--text-2); width: 3.5em; flex-shrink: 0; }
.tag-chip {
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: 999px;
  color: var(--text-1);
  font-size: 11.5px;
  padding: 3px 9px;
  cursor: pointer;
  white-space: nowrap;
}
.tag-chip:hover { color: var(--text-0); border-color: var(--line-strong); }
.tag-chip.on { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.tag-chip em { font-style: normal; font-size: 10px; color: var(--text-2); margin-left: 5px; }
.tag-tools { display: flex; justify-content: flex-end; align-items: center; gap: 12px; min-height: 18px; }
.tag-hint { font-size: 11px; color: var(--text-2); }
.tag-clear { background: none; border: none; color: var(--text-2); cursor: pointer; font-size: 11.5px; }
.tag-clear:hover { color: var(--accent-cyan); }

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
