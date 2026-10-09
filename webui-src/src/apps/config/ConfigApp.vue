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
 * 分类显示名：键 = 后端 Patches/<分类>/ 目录名（协议 category 字段），前端只认这套
 * 稳定的目录名集合并给中文标签，未知值回退原名；段名本身仍零硬编码。
 * 右键重命名走 useSectionMeta（localStorage 备忘），与功能段图标/改名同一套机制。
 */
const CATEGORY_LABELS = {
  Dev: '开发类',
  Experience: '体验增强',
  Fun: '娱乐功能',
  Shop: '零元购',
  System: '游戏系统',
};
/** 已知分类的固定展示顺序；未知分类按名排其后，「其他」（无 category 的基础设施段）恒最后。 */
const CATEGORY_ORDER = ['Dev', 'Experience', 'Fun', 'Shop', 'System'];
const OTHER_KEY = '__other';   // 无分类段的归并键（后端 category 缺省）

/**
 * 分类说明：列表显示模式下每行右侧的那一句（说明这个分类是干什么的）。
 * 与 CATEGORY_LABELS 同一套路：只认稳定的目录名键，未知分类**留空**而不是编造。
 * 注：「捉迷藏」是 HideAndSeek 通过反射登记进上游 SectionCategories 的分类，
 * 上游源码里没有它，所以说明也只能在这里给。
 */
const CATEGORY_DESCS = {
  Dev: '开发调试用，正常游玩不需要开。',
  Experience: '改善操作手感与信息可读性，不改动胜负规则。',
  Fun: '娱乐性功能，与平衡无关。',
  Shop: '商店、角色与表情的解锁相关。',
  System: '房间、流程与游戏系统层面的调整。',
  捉迷藏: 'HideAndSeek 捉迷藏玩法的房主端功能与参数。',
};

function categoryLabel(key) {
  if (key === OTHER_KEY) return '其他';
  return CATEGORY_LABELS[key] || key;
}
function categoryDesc(key) {
  if (key === OTHER_KEY) return '不属于以上分类的基础设施配置段。';
  return CATEGORY_DESCS[key] || '';
}
function categoryRank(key) {
  const i = CATEGORY_ORDER.indexOf(key);
  if (i !== -1) return i;
  return key === OTHER_KEY ? CATEGORY_ORDER.length + 1 : CATEGORY_ORDER.length;
}
/** 段 → 归并分类键（协议 category 或 OTHER_KEY）。 */
function categoryOf(s) { return s.category || OTHER_KEY; }

const sections = ref([]);
const query = ref('');
const openCategory = ref(null);  // 展开中的分类键，null=分类网格视图
const openSection = ref(null);   // 展开中的段名，null=未进入段详情
const sectionMeta = useSectionMeta();

// 网格/列表视图状态与切换：与自动化页**共用一份**（切换按钮在各自页面的工具栏上）
const { viewMode, toggleView } = useFolderView();
/** 根视图且无搜索 = 分类列表（行右侧给分类说明）；否则是段列表（给段摘要）。 */
const isCategoryView = computed(() => !openCategory.value && !query.value.trim());

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
function closeSection() {
  openSection.value = null;
  currentSection.value = null;
}
/** 段详情返回：有上级分类回到分类文件夹，从搜索结果进入的回根视图。 */
function backToFolder() {
  closeSection();
}
function backToRoot() {
  closeSection();
  openCategory.value = null;
}

/** FolderGrid 的 open：根视图无搜索时点的是分类磁贴，其余都是段磁贴。 */
function onOpen(item) {
  if (!openCategory.value && !query.value.trim()) {
    openCategory.value = item.key;
    return;
  }
  openDetail(item);
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

/** 段磁贴摘要（key=段名，FolderGrid 经 useSectionMeta 显示别名/图标）。 */
function toSectionTile(s) {
  return {
    key: s.section,
    label: s.section,
    count: (s.entries || []).length,
    enabled: enabledOf(s),
  };
}

function matchSection(s, q) {
  if (!q) return true;
  const alias = (sectionMeta.displayName(s.section) || '').toLowerCase();
  // 分类中文标签也纳入搜索：搜「商店」应列出商店分类下的功能
  const catLabel = categoryLabel(categoryOf(s)).toLowerCase();
  return alias.includes(q) || catLabel.includes(q) || sectionMatches(s, q);
}

/** 当前分类下的功能段（分类视图用）。 */
const sectionsInCategory = computed(() =>
  featureSections.value.filter(s => categoryOf(s) === openCategory.value));

/** 根视图的分类磁贴：按固定顺序排列，无分类段归并进「其他」（空则不出现）。 */
const categoryItems = computed(() => {
  const counts = new Map();
  for (const s of featureSections.value) {
    const key = categoryOf(s);
    counts.set(key, (counts.get(key) || 0) + 1);
  }
  return [...counts.keys()]
    .sort((a, b) => categoryRank(a) - categoryRank(b) || (a < b ? -1 : 1))
    .map(key => ({ key, label: categoryLabel(key), count: counts.get(key), enabled: null }));
});

const folderItems = computed(() => {
  const q = query.value.trim().toLowerCase();
  if (openCategory.value) {
    // 分类视图：只列本分类的功能段
    return sectionsInCategory.value.filter(s => matchSection(s, q)).map(toSectionTile);
  }
  if (q) {
    // 根视图搜索：全部分类内的功能段平铺，磁贴直接进详情
    return featureSections.value.filter(s => matchSection(s, q)).map(toSectionTile);
  }
  return categoryItems.value;
});

function enabledOf(section) {
  // 开关键名走协议字段（后端 Engine.EnabledKey），不硬编码 'Enabled'
  const key = section.enabledKey || 'Enabled';
  const e = (section.entries || []).find(en => en.key === key);
  return e ? !!e.value : null;
}

/**
 * 段摘要 = 该段 Enabled 项的 description。
 *
 * 功能说明本来就写在那儿（上游与 HideAndSeek 绑 Enabled 时都把 [PatchFeature] 的描述带过去），
 * 所以列表模式既不用让后端补字段，也不用前端维护第二份文案 —— 加功能时自动跟上。
 */
/**
 * 列表行的右侧说明：分类行给分类说明，段行给**功能说明**。
 *
 * 段说明取自该段开关键（enabledKey）的描述，并用上游既有的 splitDesc 剥掉
 * 「Author: … / Side: …」元数据行 —— 直接用原文的话，列表里整列都是作者名。
 */
function descOf(item) {
  if (isCategoryView.value) return categoryDesc(item.key);

  const s = sections.value.find(x => x.section === item.key);
  if (!s) return '';
  const key = s.enabledKey || 'Enabled';
  const e = (s.entries || []).find(en => en.key === key);
  const text = splitDesc(e && e.description).text;
  return text.split('\n').map(t => t.trim()).filter(Boolean)[0] || '';
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
        <template v-if="openSection">
          <span class="breadcrumb">
            <a class="crumb" title="返回全部分类" @click="backToRoot">全部配置</a>
            <template v-if="openCategory">
              <span class="sep">/</span>
              <a class="crumb" :title="'返回 ' + categoryLabel(openCategory)" @click="closeSection">{{ categoryLabel(openCategory) }}</a>
            </template>
            <span class="sep">/</span>
            <b :title="openSection">{{ sectionMeta.displayName(openSection) || openSection }}</b>
          </span>
        </template>
        <template v-else-if="openCategory">
          <span class="breadcrumb">
            <a class="crumb" title="返回全部分类" @click="backToRoot">全部配置</a>
            <span class="sep">/</span>
            <b>{{ categoryLabel(openCategory) }}</b>
          </span>
          <input v-model="query" class="search" type="search" placeholder="搜索本分类功能 / 字段…">
        </template>
        <input v-else v-model="query" class="search" type="search" placeholder="搜索功能 / 分类 / 字段…">
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

    <div v-if="toast" class="toast" :class="{ error: toast.error }">{{ toast.text }}</div>

    <FolderGrid v-if="!openSection" :items="folderItems" :desc-of="descOf" @open="onOpen" />
    <SectionDetail
      v-if="currentSection"
      :section="currentSection"
      :back-label="openCategory ? categoryLabel(openCategory) : '全部配置'"
      @back="backToFolder"
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
