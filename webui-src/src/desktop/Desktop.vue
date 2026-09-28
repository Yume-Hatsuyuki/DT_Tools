<script setup>
import { ref, markRaw, computed, onMounted, onUnmounted } from 'vue';
import Taskbar from './Taskbar.vue';
import DesktopIcon from './DesktopIcon.vue';
import Window from './Window.vue';
import CropperHost from '../apps/common/CropperHost.vue';
import { useWindowManager } from '../composables/useWindowManager.js';
import { useSectionMeta } from '../composables/useSectionMeta.js';
import { pickImageFile } from '../composables/pickImage.js';
import { useCropper } from '../composables/useCropper.js';
import { useWallpaper } from '../composables/useWallpaper.js';
// 仅调用 useConsole() 触发日志流单例启动：连接状态（任务栏）从桌面加载起就由
// SSE/轮询驱动，不依赖用户是否打开过控制台窗口。
import { useConsole } from '../apps/console/useConsole.js';

import ConsoleApp from '../apps/console/ConsoleApp.vue';
import TerminalApp from '../apps/console/TerminalApp.vue';
import ConfigApp from '../apps/config/ConfigApp.vue';
import AutomationApp from '../apps/automation/AutomationApp.vue';

import IconTerminal2 from '~icons/tabler/terminal-2';
import IconDragon from '~icons/tabler/dragon';
import IconAdjustmentsHorizontal from '~icons/tabler/adjustments-horizontal';
import IconRobot from '~icons/tabler/robot';
import IconTerminal from '~icons/tabler/terminal';
import IconPhoto from '~icons/tabler/photo';
import IconPhotoOff from '~icons/tabler/photo-off';
import IconInfoCircle from '~icons/tabler/info-circle';

/**
 * 桌面应用注册表。id 同时是备忘录键（app:<id>）与窗口布局键；
 * 终端有两套：terminal=Kali 风格新版（不带 / 前缀），console=旧版（/ 命令 + 发送按钮）。
 */
const apps = [
  { id: 'terminal', title: '控制台', icon: markRaw(IconDragon), component: markRaw(TerminalApp), defaults: { x: 60, y: 50, w: 760, h: 520 } },
  { id: 'console', title: '控制台（旧版）', icon: markRaw(IconTerminal2), component: markRaw(ConsoleApp), defaults: { x: 90, y: 80, w: 760, h: 520 } },
  { id: 'config', title: 'DT 配置', icon: markRaw(IconAdjustmentsHorizontal), component: markRaw(ConfigApp), defaults: { x: 140, y: 110, w: 860, h: 580 } },
  { id: 'automation', title: '自动化', icon: markRaw(IconRobot), component: markRaw(AutomationApp), defaults: { x: 180, y: 140, w: 720, h: 520 } },
];

useConsole();

const { windows, open, close, focus, minimize, toggleFocus, toggleMaximize, updateGeometry } = useWindowManager();
const metaStore = useSectionMeta();
const cropper = useCropper();
const wallpaper = useWallpaper();

const selectedIcon = ref(null);          // app:<id> 或 null
const iconMenu = ref(null);              // { x, y, appKey }
const desktopMenu = ref(null);           // { x, y }
const busyInfo = ref(false);

function appMetaKey(appId) { return 'app:' + appId; }
function appLabel(app) { return metaStore.displayName(appMetaKey(app.id)) || app.title; }
function appIconSrc(app) { return (metaStore.get(appMetaKey(app.id)) || {}).icon || null; }

/** 多实例：每次启动都开新窗口（任务栏标签负责找回已开窗口）。 */
function launchApp(appId) {
  const app = apps.find(a => a.id === appId);
  if (!app) return;
  const win = open(appId, { title: appLabel(app), icon: app.icon, defaults: app.defaults });
  win.appDef = markRaw(app);
}

// ---- 图标右键菜单 ----

function onIconContextMenu(e, appId) {
  e.preventDefault();
  e.stopPropagation();
  desktopMenu.value = null;
  selectedIcon.value = appId;
  iconMenu.value = {
    x: Math.min(e.clientX, window.innerWidth - 190),
    y: Math.min(e.clientY, window.innerHeight - 220),
    appId,
  };
}
function closeMenus() { iconMenu.value = null; desktopMenu.value = null; }

const iconMenuItems = computed(() => {
  const appId = iconMenu.value?.appId;
  if (!appId) return [];
  const app = apps.find(a => a.id === appId);
  const key = appMetaKey(appId);
  const items = [
    { label: '打开', action: () => { closeMenus(); launchApp(appId); } },
    { label: '重命名…', action: () => renameApp(app) },
  ];
  if (metaStore.displayName(key)) items.push({ label: '恢复原名', action: () => { metaStore.setName(key, ''); closeMenus(); } });
  items.push({ label: '自定义图标…', action: () => changeAppIcon(app) });
  if (appIconSrc(app)) items.push({ label: '恢复默认图标', action: () => { metaStore.clearIcon(key); closeMenus(); } });
  return items;
});

async function renameApp(app) {
  closeMenus();
  const key = appMetaKey(app.id);
  const name = prompt('重命名（仅 WebUI 备忘，留空恢复原名）：', metaStore.displayName(key) || '');
  if (name === null) return;
  metaStore.setName(key, name);
}

async function changeAppIcon(app) {
  closeMenus();
  const file = await pickImageFile();
  if (!file) return;
  const dataUrl = await cropper.open(file, { title: '裁切应用图标', shape: 'square' });
  if (!dataUrl) return;
  const err = metaStore.setIcon(appMetaKey(app.id), dataUrl);
  if (err) alert(err);
}

// ---- 桌面右键菜单 ----

function onDesktopContextMenu(e) {
  // 窗口内部右键不弹桌面菜单（浏览器菜单已在全局捕获阶段屏蔽）
  if (e.target.closest && e.target.closest('.win')) return;
  e.preventDefault();
  iconMenu.value = null;
  desktopMenu.value = {
    x: Math.min(e.clientX, window.innerWidth - 190),
    y: Math.min(e.clientY, window.innerHeight - 220),
  };
}

async function changeWallpaper() {
  closeMenus();
  const file = await pickImageFile();
  if (!file) return;
  const err = await wallpaper.setCustom(file);
  if (err) alert(err.error);
}
function resetWallpaper() {
  closeMenus();
  wallpaper.reset();
}

// ---- 窗口内容屏蔽浏览器右键菜单（输入框/文本域除外，保留粘贴菜单）----

function onGlobalContextMenu(e) {
  const t = e.target;
  if (!t.closest || !t.closest('.win')) return;                 // 桌面/图标有自己的菜单逻辑
  if (t.matches('input, textarea') || t.isContentEditable) return; // 保留系统粘贴菜单
  e.preventDefault();
}

// ---- 桌面壁纸样式 ----

const desktopStyle = computed(() => {
  const w = wallpaper.state;
  if (w.custom) {
    return { backgroundImage: `url(${w.custom})`, backgroundSize: 'cover', backgroundPosition: 'center' };
  }
  const preset = wallpaper.presets.find(p => p.id === w.presetId) || wallpaper.presets[0];
  if (preset.image) {
    return { backgroundImage: `url(${preset.image})`, backgroundSize: 'cover', backgroundPosition: 'center' };
  }
  const style = { backgroundImage: preset.css };
  if (preset.size) style.backgroundSize = preset.size;
  return style;
});

function windowFor(win) {
  return win.appDef || apps.find(a => a.id === win.appId);
}

onMounted(() => document.addEventListener('contextmenu', onGlobalContextMenu, true));
onUnmounted(() => document.removeEventListener('contextmenu', onGlobalContextMenu, true));
</script>

<template>
  <div class="desktop" :style="desktopStyle" @click="closeMenus(); selectedIcon = null" @contextmenu="onDesktopContextMenu">
    <div class="desktop-grid">
      <DesktopIcon
        v-for="app in apps"
        :key="app.id"
        :label="appLabel(app)"
        :icon="app.icon"
        :custom-src="appIconSrc(app)"
        :selected="selectedIcon === app.id"
        @select.stop="selectedIcon = app.id"
        @open="launchApp(app.id)"
        @contextmenu="onIconContextMenu($event, app.id)"
      />
    </div>

    <div
      v-if="iconMenu"
      class="ctx-menu"
      :style="{ left: iconMenu.x + 'px', top: iconMenu.y + 'px' }"
      @click.stop
      @contextmenu.prevent
    >
      <button v-for="mi in iconMenuItems" :key="mi.label" type="button" @click="mi.action()">{{ mi.label }}</button>
    </div>

    <div
      v-if="desktopMenu"
      class="ctx-menu"
      :style="{ left: desktopMenu.x + 'px', top: desktopMenu.y + 'px' }"
      @click.stop
      @contextmenu.prevent
    >
      <button type="button" @click="launchApp('terminal'); closeMenus()"><IconTerminal /> 打开终端</button>
      <button type="button" @click="changeWallpaper"><IconPhoto /> 更换壁纸…</button>
      <button type="button" @click="resetWallpaper"><IconPhotoOff /> 恢复默认壁纸</button>
      <button type="button" :disabled="busyInfo" @click="busyInfo = true; closeMenus()"><IconInfoCircle /> 关于</button>
    </div>

    <Window
      v-for="win in windows"
      :key="win.id"
      :win="win"
      @close="close(win.id)"
      @focus="focus(win.id)"
      @minimize="minimize(win.id)"
      @toggle-maximize="toggleMaximize(win.id)"
      @update-geometry="patch => updateGeometry(win.id, patch)"
    >
      <component :is="windowFor(win).component" v-if="windowFor(win)" />
    </Window>

    <Taskbar :windows="windows" :apps="apps" @focus-window="toggleFocus" @launch="launchApp" />

    <CropperHost />

    <div v-if="busyInfo" class="about-mask" @click.self="busyInfo = false" @contextmenu.prevent>
      <div class="about-box">
        <div class="about-title">DT_Tools WebUI</div>
        <div class="about-line">Deadly Trick 游戏工具插件 · 桌面壳（Kali 主题）</div>
        <div class="about-line">所有配置 / 重命名 / 壁纸 / 图标仅存于本浏览器。</div>
        <button type="button" class="about-close" @click="busyInfo = false">关闭</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.desktop {
  position: fixed;
  inset: 0;
  background-color: var(--desktop-bg-0);
  background-size: cover;
  background-position: center;
  overflow: hidden;
}

.desktop-grid {
  position: absolute;
  top: 20px;
  left: 16px;
  display: flex;
  flex-direction: column;
  flex-wrap: wrap;
  gap: 4px;
  height: calc(100% - 90px);
  align-content: flex-start;
}

.ctx-menu {
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
.ctx-menu button {
  display: flex;
  align-items: center;
  gap: 8px;
  background: transparent;
  border: none;
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  cursor: pointer;
}
.ctx-menu button svg { width: 14px; height: 14px; color: var(--accent-cyan); flex-shrink: 0; }
.ctx-menu button:hover { background: var(--surface-2); color: var(--text-0); }
.ctx-menu button:disabled { opacity: 0.5; cursor: default; }

.about-mask {
  position: fixed;
  inset: 0;
  z-index: 10002;
  background: rgba(0, 0, 0, 0.5);
  display: grid;
  place-items: center;
}
.about-box {
  width: 340px;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--win-shadow);
  padding: 16px;
  text-align: center;
}
.about-title { font-size: 15px; font-weight: 700; color: var(--accent-cyan); font-family: var(--font-mono); margin-bottom: 8px; }
.about-line { font-size: 12px; color: var(--text-1); line-height: 1.7; }
.about-close {
  margin-top: 12px;
  background: var(--accent-blue-btn);
  border: none;
  border-radius: var(--radius-sm);
  color: #fff;
  font-size: 12px;
  padding: 6px 18px;
  cursor: pointer;
}
.about-close:hover { filter: brightness(1.1); }
</style>
