<script setup>
import { ref, computed } from 'vue';
import { useSectionMeta } from '../composables/useSectionMeta.js';
import { pickImageFile } from '../composables/pickImage.js';
import { useCropper } from '../composables/useCropper.js';

/**
 * 底部应用坞（macOS Dock 风）：桌面应用图标全部整合于此——悬停鱼眼放大、
 * 上方气泡提示、运行中窗口指示点；单击打开/聚焦（已开窗口在 聚焦/最小化
 * 间切换），右键备忘菜单（重命名/自定义图标，仅存本浏览器）。
 * 菜单 Teleport 到 body：Dock 容器的 backdrop-filter 会劫持 fixed 后代的
 * 包含块，留在内部会按 Dock 左上角错位（FolderGrid 同款先例）。
 */
const props = defineProps({
  /** 桌面应用注册表 [{id,title,icon}]。 */
  apps: { type: Array, required: true },
  /** 运行中窗口（算运行指示点 + 聚焦切换）。 */
  windows: { type: Array, required: true },
  /** 小组件（插件弹窗）定义 [{id,title,icon}]，排在应用图标之后。 */
  widgets: { type: Array, default: () => [] },
  /** 当前打开的小组件 id 集合（指示点）。 */
  openWidgets: { type: Array, default: () => [] },
});
const emit = defineEmits(['launch', 'focus-window', 'toggle-widget']);

const metaStore = useSectionMeta();
const cropper = useCropper();

/** Dock 全部条目（应用 + 小组件），鱼眼缩放按这份列表算。 */
const entries = computed(() => [
  ...props.apps.map(a => ({ kind: 'app', id: a.id, title: a.title, icon: a.icon })),
  ...props.widgets.map(w => ({ kind: 'widget', id: w.id, title: w.title, icon: w.icon })),
]);

function appMetaKey(appId) { return 'app:' + appId; }
function appLabel(app) { return metaStore.displayName(appMetaKey(app.id)) || app.title; }
function appIconSrc(app) { return (metaStore.get(appMetaKey(app.id)) || {}).icon || null; }

/** 该应用运行中的窗口（按 z 降序，第一扇即最上层）。 */
function windowsOf(appId) {
  return props.windows
    .filter(w => w.appId === appId)
    .sort((a, b) => b.z - a.z);
}

function onItem(entry) {
  if (entry.kind === 'widget') {
    emit('toggle-widget', entry.id);
    return;
  }
  const wins = windowsOf(entry.id);
  if (wins.length) emit('focus-window', wins[0].id);
  else emit('launch', entry.id);
}

// ---- 悬停鱼眼：鼠标 x 距各图标中心的距离映射为放大倍率 ----

const itemEls = ref([]);
const scales = ref([]);
function ensureScales() {
  if (scales.value.length !== entries.value.length) {
    scales.value = entries.value.map(() => 1);
  }
  return scales.value;
}
const FISH_RADIUS = 120;
const FISH_MAX = 0.42;

function onDockMove(e) {
  const s = ensureScales();
  itemEls.value.forEach((el, i) => {
    if (!el) { s[i] = 1; return; }
    const r = el.getBoundingClientRect();
    const d = Math.abs(e.clientX - (r.left + r.width / 2));
    s[i] = d >= FISH_RADIUS ? 1 : 1 + FISH_MAX * Math.pow(1 - d / FISH_RADIUS, 1.7);
  });
}
function onDockLeave() {
  scales.value = entries.value.map(() => 1);
  hoverIdx.value = -1;
}

function itemStyle(i) {
  const s = ensureScales()[i] || 1;
  return {
    transform: `translateY(${(-(s - 1) * 26).toFixed(2)}px) scale(${s.toFixed(3)})`,
  };
}

/** 小组件是否打开（运行指示点）。 */
function widgetOpen(id) {
  return props.openWidgets.includes(id);
}

function appOf(id) {
  return props.apps.find(a => a.id === id);
}
function entryLabel(entry) {
  return entry.kind === 'app' ? appLabel(appOf(entry.id)) : entry.title;
}
function entryIconSrc(entry) {
  return entry.kind === 'app' ? appIconSrc(appOf(entry.id)) : null;
}
function entryRunning(entry) {
  return entry.kind === 'app' ? windowsOf(entry.id).length > 0 : widgetOpen(entry.id);
}
function onEntryContextMenu(e, entry) {
  e.preventDefault();
  if (entry.kind !== 'app') return;   // 小组件无备忘菜单，仅屏蔽浏览器菜单
  openMenu(e, appOf(entry.id));
}

// ---- tooltip：悬停项的气泡提示（图2 风格：图标上方黑底小气泡）----

const hoverIdx = ref(-1);

// ---- 右键备忘菜单（原桌面图标菜单迁入）----

const menu = ref(null);   // { x, y, appId }

function openMenu(e, app) {
  e.preventDefault();
  e.stopPropagation();
  menu.value = {
    x: Math.min(e.clientX, window.innerWidth - 190),
    y: Math.min(e.clientY, window.innerHeight - 230),
    appId: app.id,
  };
}
function closeMenu() { menu.value = null; }

const menuItems = computed(() => {
  const appId = menu.value?.appId;
  if (!appId) return [];
  const app = props.apps.find(a => a.id === appId);
  const key = appMetaKey(appId);
  const items = [
    { label: '打开', action: () => { closeMenu(); emit('launch', appId); } },
    { label: '重命名…', action: () => renameApp(app) },
  ];
  if (metaStore.displayName(key)) items.push({ label: '恢复原名', action: () => { metaStore.setName(key, ''); closeMenu(); } });
  items.push({ label: '自定义图标…', action: () => changeAppIcon(app) });
  if (appIconSrc(app)) items.push({ label: '恢复默认图标', action: () => { metaStore.clearIcon(key); closeMenu(); } });
  return items;
});

async function renameApp(app) {
  closeMenu();
  const key = appMetaKey(app.id);
  const name = prompt('重命名（仅 WebUI 备忘，留空恢复原名）：', metaStore.displayName(key) || '');
  if (name === null) return;
  metaStore.setName(key, name);
}

async function changeAppIcon(app) {
  closeMenu();
  const file = await pickImageFile();
  if (!file) return;
  const dataUrl = await cropper.open(file, { title: '裁切应用图标', shape: 'square' });
  if (!dataUrl) return;
  const err = metaStore.setIcon(appMetaKey(app.id), dataUrl);
  if (err) alert(err);
}
</script>

<template>
  <div class="dock-wrap" @mousemove="onDockMove" @mouseleave="onDockLeave" @click="closeMenu">
    <div class="dock">
      <template v-for="(entry, i) in entries" :key="entry.kind + ':' + entry.id">
        <!-- 应用与小组件之间加细分隔线（macOS Dock 惯例） -->
        <div v-if="entry.kind === 'widget'" class="dock-sep" />
        <div
          :ref="el => (itemEls[i] = el)"
          class="dock-item"
          @click.stop="onItem(entry)"
          @contextmenu="onEntryContextMenu($event, entry)"
          @mouseenter="hoverIdx = i"
        >
          <div v-if="hoverIdx === i" class="dock-tip">{{ entryLabel(entry) }}</div>
          <div class="dock-icon" :style="itemStyle(i)">
            <img v-if="entryIconSrc(entry)" :src="entryIconSrc(entry)" :alt="entryLabel(entry)" class="dock-icon-img">
            <component :is="entry.icon" v-else class="dock-icon-fallback" />
          </div>
          <span class="dock-dot" :class="{ running: entryRunning(entry) }" />
        </div>
      </template>
    </div>

    <Teleport to="body">
      <div v-if="menu" class="dock-menu-mask" @click="closeMenu" @contextmenu.prevent="closeMenu" />
      <div
        v-if="menu"
        class="dock-menu"
        :style="{ left: menu.x + 'px', top: menu.y + 'px' }"
        @click.stop
        @contextmenu.prevent
      >
        <button v-for="mi in menuItems" :key="mi.label" type="button" @click="mi.action()">{{ mi.label }}</button>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.dock-wrap {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  justify-content: center;
  z-index: 5000;
  pointer-events: none;   /* 只有 Dock 本体可点，两侧留空给窗口拖放 */
}

.dock {
  pointer-events: auto;
  display: flex;
  align-items: flex-end;
  gap: 10px;
  padding: 9px 12px 13px;
  margin-bottom: 8px;
  background: var(--dock-bg);
  border: 1px solid var(--dock-border);
  border-radius: 20px;
  box-shadow: 0 18px 48px -12px rgba(2, 10, 18, 0.7), inset 0 1px 0 rgba(255, 255, 255, 0.06);
  backdrop-filter: blur(24px) saturate(160%);
}

.dock-item {
  position: relative;
  display: flex;
  justify-content: center;
  width: 52px;
  height: 52px;
  cursor: pointer;
  -webkit-tap-highlight-color: transparent;
}

/* 应用区与小组件区的细分隔线 */
.dock-sep {
  width: 1px;
  align-self: stretch;
  margin: 6px 2px;
  background: var(--dock-border);
}

.dock-icon {
  width: 46px;
  height: 46px;
  display: grid;
  place-items: center;
  border-radius: 13px;
  /* 深海青玻璃瓦片：与壳层同源的高光青渐变 + 白描边，随鱼眼一起缩放 */
  background: radial-gradient(circle at 50% 40%,
    rgba(120, 210, 240, 0.30) 0%,
    rgba(52, 120, 160, 0.42) 48%,
    rgba(14, 38, 56, 0.85) 100%);
  border: 1px solid rgba(190, 235, 255, 0.22);
  box-shadow: 0 8px 20px rgba(2, 10, 18, 0.55), inset 0 1px 0 rgba(255, 255, 255, 0.14);
  transition: transform 0.13s var(--ease), box-shadow 0.13s var(--ease);
  will-change: transform;
}
.dock-item:hover .dock-icon {
  border-color: rgba(190, 235, 255, 0.45);
  box-shadow: 0 12px 30px rgba(2, 10, 18, 0.65), 0 0 14px var(--shell-glow-dim), inset 0 1px 0 rgba(255, 255, 255, 0.2);
}
.dock-icon-fallback {
  width: 24px;
  height: 24px;
  color: #dff6ff;
  filter: drop-shadow(0 1px 3px rgba(2, 10, 18, 0.8));
}
.dock-icon-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 13px;
}

/* 运行指示点：macOS 式图标下方小圆点 */
.dock-dot {
  position: absolute;
  bottom: -7px;
  left: 50%;
  transform: translateX(-50%);
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: transparent;
}
.dock-dot.running {
  background: var(--shell-glow);
  box-shadow: 0 0 6px var(--shell-glow-dim);
}

/* 悬停气泡提示（图2 终端 tooltip 风格） */
.dock-tip {
  position: absolute;
  bottom: calc(100% + 12px);
  left: 50%;
  transform: translateX(-50%);
  background: rgba(8, 15, 22, 0.92);
  border: 1px solid var(--dock-border);
  color: var(--text-0);
  font-size: 11.5px;
  line-height: 1;
  padding: 6px 10px;
  border-radius: 7px;
  white-space: nowrap;
  pointer-events: none;
  box-shadow: 0 6px 18px rgba(2, 10, 18, 0.5);
  z-index: 2;
}
.dock-tip::after {
  content: '';
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  border: 5px solid transparent;
  border-top-color: rgba(8, 15, 22, 0.92);
}

/* 右键备忘菜单（Teleport 到 body） */
.dock-menu-mask {
  position: fixed;
  inset: 0;
  z-index: 9998;
}
.dock-menu {
  position: fixed;
  z-index: 9999;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-sm);
  box-shadow: var(--menu-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  padding: 4px;
  display: flex;
  flex-direction: column;
  min-width: 170px;
}
.dock-menu button {
  background: transparent;
  border: none;
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  cursor: pointer;
}
.dock-menu button:hover { background: rgba(150, 225, 255, 0.10); color: var(--text-0); }
</style>
