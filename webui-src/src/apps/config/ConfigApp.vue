<script setup>
import { ref, onMounted, onUnmounted, computed, onBeforeUnmount } from 'vue';
import { API } from '../../api.js';
import { t } from '../../i18n/index.js';
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
import IconChevronDown from '~icons/tabler/chevron-down';
import IconChevronUp from '~icons/tabler/chevron-up';

/**
 * 功能标签字典（分组 / 键 / 中文名 / 说明）由后端 FeatureTagCatalog 给出（/api/config/tags）。
 * 标签的成员与键由后端决定；译文在 i18n 总表（tag.label.<键> / tag.desc.<键> / tag.group.<分组键>）。
 * 新增标签时须同步补译文，缺译时回退后端给出的中文。
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

const tagByKey = computed(() => new Map(tagCatalog.value.map(tag => [tag.key, tag])));

/** t() 找不到键时返回键名本身；此时改用后端给出的中文，保证新增标签未译时仍可读。 */
function localized(key, fallback) {
  const text = t(key);
  return text === key ? fallback : text;
}
function tagLabel(key) {
  const tag = tagByKey.value.get(key);
  return tag ? localized(`tag.label.${key}`, tag.label) : key;
}
function tagTitle(tag) {
  return t('config.tags.itemTitle', { label: tagLabel(tag.key), desc: localized(`tag.desc.${tag.key}`, tag.description) });
}
function sideLabel(side) { return t(SIDE_KEY[side]); }

/** 标签按领域分组（分组键来自后端 groupKey），保持字典顺序。 */
const tagGroups = computed(() => {
  const groups = new Map();
  for (const tag of tagCatalog.value) {
    if (!groups.has(tag.groupKey)) groups.set(tag.groupKey, { key: tag.groupKey, name: tag.group, tags: [] });
    groups.get(tag.groupKey).tags.push(tag);
  }
  return [...groups.values()];
});
function groupName(g) { return localized(`tag.group.${g.key}`, g.name); }

/** 功能段（不含自动化段）。 */
const featureSections = computed(() => sections.value.filter(s => s.group !== 'automation'));

/** 每个标签下有多少功能段（芯片上的计数）。 */
const tagCounts = computed(() => {
  const counts = new Map();
  for (const s of featureSections.value) {
    for (const tag of s.tags || []) counts.set(tag, (counts.get(tag) || 0) + 1);
  }
  return counts;
});

/* ---- 标签栏：隐藏未使用标签、自由调高、收起 ---- */
const TAG_BAR_HEIGHT_KEY = 'dt.config.tagBarHeight';
const TAG_BAR_COLLAPSED_KEY = 'dt.config.tagBarCollapsed';
const TAG_BAR_MIN = 96;   // 至少能容下状态与作用面两行，再小就看不清了
const SIDE_KEY = { client: 'config.tags.sideClient', host: 'config.tags.sideHost', both: 'config.tags.sideBoth' };

/** 读写失败（存储被禁用等）时只是不记忆，不影响筛选功能本身。 */
function readStored(key) {
  try { return localStorage.getItem(key); } catch (e) { return null; }
}
function writeStored(key, value) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, String(value));
  } catch (e) { /* 忽略 */ }
}

const storedHeight = Number(readStored(TAG_BAR_HEIGHT_KEY));
const tagBarHeight = ref(readStored(TAG_BAR_HEIGHT_KEY) !== null && Number.isFinite(storedHeight) && storedHeight > 0 ? storedHeight : null);
const tagBarCollapsed = ref(readStored(TAG_BAR_COLLAPSED_KEY) === '1');
const tagBarEl = ref(null);
const rootEl = ref(null);
/* 窗口尺寸变化时递增，让依赖配置页高度的样式重新计算 */
const viewTick = ref(0);
function onWindowResize() { viewTick.value++; }
onMounted(() => window.addEventListener('resize', onWindowResize));

/**
 * 上限取配置页自身高度的 70%。不能用视口高度：配置页是桌面窗口，比视口矮，
 * 按视口计算会把分隔条推到窗口下方，鼠标和键盘都够不着。
 */
function tagBarMax() {
  const h = rootEl.value ? rootEl.value.clientHeight : window.innerHeight;
  return Math.round(h * 0.7);
}
function clampTagBarHeight(h) { return Math.max(TAG_BAR_MIN, Math.min(tagBarMax(), Math.round(h))); }

/** null 表示自动高度（由内容决定），与首次打开的默认外观一致。 */
const tagBarStyle = computed(() => {
  void viewTick.value;
  if (tagBarCollapsed.value || tagBarHeight.value === null) return {};
  return { height: clampTagBarHeight(tagBarHeight.value) + 'px' };
});

function toggleCollapsed() {
  tagBarCollapsed.value = !tagBarCollapsed.value;
  writeStored(TAG_BAR_COLLAPSED_KEY, tagBarCollapsed.value ? '1' : '0');
}

function setTagBarHeight(h) {
  tagBarHeight.value = clampTagBarHeight(h);
  writeStored(TAG_BAR_HEIGHT_KEY, tagBarHeight.value);
}
function resetTagBarHeight() {
  tagBarHeight.value = null;
  writeStored(TAG_BAR_HEIGHT_KEY, null);
}

/* 拖动分隔条：指针捕获保证鼠标移出分隔条时仍能继续拖动；松开时才写入存储，避免拖动中频繁写盘 */
let resizeFrom = null;
function onResizeStart(e) {
  if (e.button !== 0) return;
  const bar = tagBarEl.value;
  resizeFrom = { y: e.clientY, h: bar ? bar.getBoundingClientRect().height : TAG_BAR_MIN };
  e.currentTarget.setPointerCapture(e.pointerId);
  window.addEventListener('pointermove', onResizeMove);
  window.addEventListener('pointerup', onResizeEnd);
  e.preventDefault();
}
function onResizeMove(e) {
  if (!resizeFrom) return;
  tagBarHeight.value = clampTagBarHeight(resizeFrom.h + (e.clientY - resizeFrom.y));
}
function onResizeEnd() {
  window.removeEventListener('pointermove', onResizeMove);
  window.removeEventListener('pointerup', onResizeEnd);
  if (resizeFrom && tagBarHeight.value !== null) writeStored(TAG_BAR_HEIGHT_KEY, tagBarHeight.value);
  resizeFrom = null;
}
/* 键盘微调：方向键每次 16px，按住 Shift 为 48px */
function onResizeKey(e) {
  const step = e.shiftKey ? 48 : 16;
  const cur = tagBarEl.value ? tagBarEl.value.getBoundingClientRect().height : TAG_BAR_MIN;
  if (e.key === 'ArrowUp') setTagBarHeight(cur - step);
  else if (e.key === 'ArrowDown') setTagBarHeight(cur + step);
  else return;
  e.preventDefault();
}
onBeforeUnmount(() => {
  window.removeEventListener('pointermove', onResizeMove);
  window.removeEventListener('pointerup', onResizeEnd);
  window.removeEventListener('resize', onWindowResize);
});

/** 未被任何功能段使用的标签不显示，空分组一并隐藏；正在生效的筛选标签即使无人使用也保留，便于清除。 */
const visibleTagGroups = computed(() => tagGroups.value
  .map(g => ({ key: g.key, name: g.name, tags: g.tags.filter(tag => (tagCounts.value.get(tag.key) || 0) > 0 || activeTags.value.includes(tag.key)) }))
  .filter(g => g.tags.length > 0));

const hasActiveFilter = computed(() => activeTags.value.length > 0 || !!statusFilter.value || !!sideFilter.value);

/** 收起时显示在摘要行里的当前筛选。 */
const filterSummary = computed(() => {
  const parts = [];
  if (statusFilter.value) parts.push(t(statusFilter.value === 'on' ? 'config.tags.on' : 'config.tags.off'));
  if (sideFilter.value) parts.push(sideLabel(sideFilter.value));
  for (const k of activeTags.value) parts.push(tagLabel(k));
  return parts.length ? t('config.tags.filtered', { list: parts.join(' · ') }) : t('config.tags.none');
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
function onToast(toast) { showToast(toast.text, toast.error); }

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
  return (s.tags || []).some(tag => activeTags.value.includes(tag));
}

/**
 * 搜索匹配：别名、标签名、段内字段。
 * 标签名同时匹配“当前语言译文”与“后端中文名”：界面切到英文后，中文搜索仍然有效（与改动前一致）。
 */
function matchSection(s, q) {
  if (!q) return true;
  const alias = (sectionMeta.displayName(s.section) || '').toLowerCase();
  const tagText = (s.tags || [])
    .flatMap(key => [tagLabel(key), tagByKey.value.get(key)?.label || ''])
    .join(' ').toLowerCase();
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
  const first = text.split('\n').map(line => line.trim()).filter(Boolean)[0] || '';
  const tags = (s.tags || []).map(tagLabel);
  return tags.length ? t('config.tags.linePrefix', { tags: tags.join(' · '), text: first }) : first;
}

onMounted(async () => {
  await loadTags();
  await reload();
  startAutoRefresh();
});
onUnmounted(stopAutoRefresh);
</script>

<template>
  <div ref="rootEl" class="config-app">
    <div class="toolbar">
      <div class="toolbar-left">
        <template v-if="openSection">
          <span class="breadcrumb">
            <a class="crumb" title="返回全部配置" @click="closeSection">全部配置</a>
            <span class="sep">/</span>
            <b :title="openSection">{{ sectionMeta.displayName(openSection) || openSection }}</b>
          </span>
        </template>
        <input v-else v-model="query" class="search" type="search" :placeholder="t('config.searchPlaceholder')">
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

    <div v-if="!openSection && tagGroups.length" class="tag-wrap">
    <div v-if="!tagBarCollapsed" ref="tagBarEl" class="tag-bar" :style="tagBarStyle">
      <div class="tag-group">
        <span class="tag-group-name">{{ t('config.tags.status') }}</span>
        <div class="tag-chips">
          <button class="tag-chip" :class="{ on: statusFilter === 'on' }" @click="toggleStatus('on')">
            {{ t('config.tags.on') }}<em>{{ statusCounts.on }}</em>
          </button>
          <button class="tag-chip" :class="{ on: statusFilter === 'off' }" @click="toggleStatus('off')">
            {{ t('config.tags.off') }}<em>{{ statusCounts.off }}</em>
          </button>
        </div>
      </div>
      <div class="tag-group">
        <span class="tag-group-name">{{ t('config.tags.side') }}</span>
        <div class="tag-chips">
          <button class="tag-chip" :class="{ on: sideFilter === 'client' }" @click="toggleSide('client')">
            {{ t('config.tags.sideClient') }}<em>{{ sideCounts.client }}</em>
          </button>
          <button class="tag-chip" :class="{ on: sideFilter === 'host' }" @click="toggleSide('host')">
            {{ t('config.tags.sideHost') }}<em>{{ sideCounts.host }}</em>
          </button>
          <button class="tag-chip" :class="{ on: sideFilter === 'both' }" @click="toggleSide('both')">
            {{ t('config.tags.sideBoth') }}<em>{{ sideCounts.both }}</em>
          </button>
        </div>
      </div>
      <div v-for="g in visibleTagGroups" :key="g.key" class="tag-group">
        <span class="tag-group-name">{{ groupName(g) }}</span>
        <div class="tag-chips">
          <button v-for="tag in g.tags" :key="tag.key"
                  class="tag-chip"
                  :class="{ on: activeTags.includes(tag.key) }"
                  :title="tagTitle(tag)"
                  @click="toggleTag(tag.key)">
            {{ tagLabel(tag.key) }}<em v-if="tagCounts.get(tag.key)">{{ tagCounts.get(tag.key) }}</em>
          </button>
        </div>
      </div>
      <div class="tag-tools">
        <span v-if="activeTags.length > 1" class="tag-hint">{{ t('config.tags.multiHint') }}</span>
        <button v-if="activeTags.length || statusFilter || sideFilter" class="tag-clear" @click="clearFilters">{{ t('config.tags.clear') }}</button>
      </div>
    </div>
    <div v-if="tagBarCollapsed" class="tag-collapsed">
      <span class="tag-collapsed-title">{{ t('config.tags.title') }}</span>
      <span class="tag-collapsed-summary" :title="filterSummary">{{ filterSummary }}</span>
      <button v-if="hasActiveFilter" class="tag-clear" @click="clearFilters">{{ t('config.tags.clear') }}</button>
      <button class="tag-toggle" :title="t('config.tags.expandTitle')" @click="toggleCollapsed"><IconChevronDown /> {{ t('config.tags.expand') }}</button>
    </div>
    <div v-else class="tag-resize"
         role="separator"
         aria-orientation="horizontal"
         :aria-label="t('config.tags.resizeLabel')"
         tabindex="0"
         :title="t('config.tags.resizeHint')"
         @pointerdown="onResizeStart"
         @dblclick="resetTagBarHeight"
         @keydown="onResizeKey">
      <span class="tag-grip"></span>
      <button class="tag-toggle" :title="t('config.tags.collapseTitle')"
              @pointerdown.stop @dblclick.stop @click.stop="toggleCollapsed"><IconChevronUp /> {{ t('config.tags.collapse') }}</button>
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

.tag-wrap { display: flex; flex-direction: column; flex-shrink: 0; border-bottom: 1px solid var(--line); }
.tag-bar {
  /* 分组名列宽取所有分组名中最宽者，各行芯片因此对齐（需 subgrid：Chrome/Edge 117+、Firefox 71+、Safari 16+） */
  display: grid;
  grid-template-columns: max-content minmax(0, 1fr);
  align-content: start;
  gap: 6px 8px;
  padding: 8px 14px;
  background: var(--surface-1);
  min-height: 0;
  overflow-y: auto;
}
.tag-resize {
  position: relative;
  height: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--surface-1);
  border-top: 1px solid var(--line);
  cursor: row-resize;
  user-select: none;
  touch-action: none;
  outline: none;
}
.tag-resize:focus-visible { box-shadow: inset 0 0 0 1px var(--accent-cyan-dim); }
.tag-grip { width: 40px; height: 3px; border-radius: 2px; background: var(--line-strong); }
.tag-resize:hover .tag-grip { background: var(--accent-cyan-dim); }
.tag-toggle {
  display: flex;
  align-items: center;
  gap: 3px;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 11px;
  color: var(--text-2);
  padding: 2px 4px;
}
.tag-toggle svg { width: 12px; height: 12px; }
.tag-toggle:hover { color: var(--text-0); }
.tag-resize .tag-toggle { position: absolute; right: 10px; top: 50%; transform: translateY(-50%); }
.tag-collapsed {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 14px;
  background: var(--surface-1);
  font-size: 12px;
}
.tag-collapsed-title { color: var(--text-2); flex-shrink: 0; }
.tag-collapsed-summary {
  flex: 1;
  min-width: 0;
  color: var(--text-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.tag-collapsed .tag-toggle { color: var(--text-1); flex-shrink: 0; }
.tag-group { grid-column: 1 / -1; display: grid; grid-template-columns: subgrid; align-items: center; }
.tag-group-name { font-size: 11px; color: var(--text-2); white-space: nowrap; }
.tag-chips { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; min-width: 0; }
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
.tag-tools { grid-column: 1 / -1; display: flex; justify-content: flex-end; align-items: center; gap: 12px; min-height: 18px; }
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
