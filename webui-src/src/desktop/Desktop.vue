<script setup>
import { ref, markRaw, computed, onMounted, onUnmounted } from 'vue';
import TopBar from './TopBar.vue';
import Dock from './Dock.vue';
import ParticleFlow from './ParticleFlow.vue';
import SteamWidget from './SteamWidget.vue';
import Window from './Window.vue';
import CropperHost from '../apps/common/CropperHost.vue';
import { useWindowManager } from '../composables/useWindowManager.js';
import { pickImageFile } from '../composables/pickImage.js';
import { useWallpaper } from '../composables/useWallpaper.js';
import { useCropper } from '../composables/useCropper.js';
// 桌面挂载即启动全局日志流（WebSocket 单例）：连接状态（顶栏）从桌面加载起
// 就由日志流驱动，不依赖任何窗口是否打开。
import { useLogStream } from '../composables/useLogStream.js';

import ConsoleApp from '../apps/console/ConsoleApp.vue';
import TerminalApp from '../apps/console/TerminalApp.vue';
import ConfigApp from '../apps/config/ConfigApp.vue';
import AutomationApp from '../apps/automation/AutomationApp.vue';
import LogApp from '../apps/log/LogApp.vue';

import IconTerminal from '~icons/tabler/terminal';
import IconPhoto from '~icons/tabler/photo';
import IconPhotoOff from '~icons/tabler/photo-off';
import IconInfoCircle from '~icons/tabler/info-circle';
import IconSparkles from '~icons/tabler/sparkles';
import IconArrowBarToDown from '~icons/tabler/arrow-bar-to-down';
import IconSquareX from '~icons/tabler/square-x';
import IconLayoutBottombarCollapse from '~icons/tabler/layout-bottombar-collapse';
import IconDragon from '~icons/tabler/dragon';
import IconTerminal2 from '~icons/tabler/terminal-2';
import IconAdjustmentsHorizontal from '~icons/tabler/adjustments-horizontal';
import IconRobot from '~icons/tabler/robot';
import IconListDetails from '~icons/tabler/list-details';
import IconBrandSteam from '~icons/tabler/brand-steam';

/**
 * 桌面应用注册表。id 同时是备忘录键（app:<id>）与窗口布局键；
 * 终端有两套：terminal=Kali 风格新版（不带 / 前缀），console=旧版（/ 命令 + 发送按钮），
 * 两套只显示各自会话的命令输出；log=全量日志应用。
 * 应用图标不再摆桌面——全部整合进底部 Dock（macOS 风），桌面只留壁纸/粒子/窗口。
 */
const apps = [
  { id: 'terminal', title: '控制台', icon: markRaw(IconDragon), component: markRaw(TerminalApp), defaults: { x: 60, y: 50, w: 760, h: 520 } },
  { id: 'console', title: '控制台（旧版）', icon: markRaw(IconTerminal2), component: markRaw(ConsoleApp), defaults: { x: 90, y: 80, w: 760, h: 520 } },
  { id: 'config', title: 'DT 配置', icon: markRaw(IconAdjustmentsHorizontal), component: markRaw(ConfigApp), defaults: { x: 140, y: 110, w: 860, h: 580 } },
  { id: 'automation', title: '自动化', icon: markRaw(IconRobot), component: markRaw(AutomationApp), defaults: { x: 180, y: 140, w: 720, h: 520 } },
  { id: 'log', title: '日志', icon: markRaw(IconListDetails), component: markRaw(LogApp), defaults: { x: 220, y: 170, w: 820, h: 560 } },
];

useLogStream();

const { windows, open, close, focus, minimize, toggleFocus, toggleMaximize, updateGeometry, showDesktop, minimizeAll, closeAll } = useWindowManager();
const wallpaper = useWallpaper();
const cropper = useCropper();

const desktopMenu = ref(null);           // { x, y } —— 桌面右键菜单
const dockMenu = ref(null);              // { x, y } —— Dock（任务栏）右键菜单
const busyInfo = ref(false);

// ---- Steam 在线小组件（Dock 可开可关，位置/关闭态由 SteamWidget 自持）----

const WIDGET_STORE_KEY = 'dt_steam_widget_v1';
function widgetClosedAtStart() {
  try { return !!(JSON.parse(localStorage.getItem(WIDGET_STORE_KEY)) || {}).closed; }
  catch { return false; }
}
const steamWidgetOpen = ref(!widgetClosedAtStart());

/** Dock 小组件定义（与应用图标同列，运行点表示弹窗开着）。 */
const widgets = [
  { id: 'steam', title: 'Steam 当前（全球）玩家数统计', icon: markRaw(IconBrandSteam) },
];

function toggleWidget(id) {
  if (id !== 'steam') return;
  steamWidgetOpen.value = !steamWidgetOpen.value;
}

/** 多实例：每次启动都开新窗口（Dock 指示点/顶栏标签负责找回已开窗口）。 */
function launchApp(appId) {
  const app = apps.find(a => a.id === appId);
  if (!app) return;
  const win = open(appId, { title: app.title, icon: app.icon, defaults: app.defaults });
  win.appDef = markRaw(app);
}

// ---- 桌面 / Dock 右键菜单 ----

function onDesktopContextMenu(e) {
  // 窗口内部右键不弹桌面菜单（浏览器菜单已在全局捕获阶段屏蔽）
  if (e.target.closest && e.target.closest('.win')) return;
  e.preventDefault();
  // 右键 Dock（任务栏）弹专属快捷菜单，不再套用桌面菜单——应用图标自己的
  // 备忘菜单在 Dock 内已 stopPropagation，不会走到这里
  if (e.target.closest && e.target.closest('.dock')) {
    desktopMenu.value = null;
    dockMenu.value = {
      x: Math.min(e.clientX, window.innerWidth - 190),
      y: Math.min(e.clientY, window.innerHeight - 200),
    };
    return;
  }
  dockMenu.value = null;
  desktopMenu.value = {
    x: Math.min(e.clientX, window.innerWidth - 190),
    y: Math.min(e.clientY, window.innerHeight - 240),
  };
}
function closeMenus() {
  desktopMenu.value = null;
  dockMenu.value = null;
}

async function changeWallpaper() {
  closeMenus();
  const file = await pickImageFile();
  if (!file) return;
  // 与头像同款裁切流程：选区比例取当前视口（裁出来即铺满桌面所见），
  // 输出 ≤1920px JPEG——落 localStorage 的体积与旧压缩管线一致
  const dataUrl = await cropper.open(file, {
    title: '裁切壁纸',
    shape: 'free',
    aspect: window.innerWidth / Math.max(1, window.innerHeight),
    output: { longEdge: 1920, mime: 'image/jpeg', quality: 0.82 },
  });
  if (!dataUrl) return;
  const err = await wallpaper.setCustom(dataUrl);
  if (err) alert(err.error);
}
function resetWallpaper() {
  closeMenus();
  wallpaper.reset();
}

// ---- 粒子效果开关（桌面壳本地状态，存 localStorage）----

// 自定义壁纸时粒子流会压在照片上，可整体关掉动效层只留壁纸（预设壁纸同理可关）
const PARTICLES_KEY = 'dt_particles_v1';
const particlesOn = ref((() => {
  try { return localStorage.getItem(PARTICLES_KEY) !== '0'; } catch { return true; }
})());
function toggleParticles() {
  particlesOn.value = !particlesOn.value;
  try { localStorage.setItem(PARTICLES_KEY, particlesOn.value ? '1' : '0'); } catch { /* 隐私模式等场景忽略 */ }
  closeMenus();
}

// ---- Dock 右键快捷菜单（动作全部在 Desktop 收口：launchApp / 窗口管理 / 关于）----

const dockMenuItems = [
  { label: '打开终端', icon: IconTerminal, action: () => { closeMenus(); launchApp('terminal'); } },
  { label: '显示桌面', icon: IconLayoutBottombarCollapse, action: () => { closeMenus(); showDesktop(); } },
  { label: '最小化所有进程', icon: IconArrowBarToDown, action: () => { closeMenus(); minimizeAll(); } },
  { label: '关闭所有进程', icon: IconSquareX, danger: true, action: () => { closeMenus(); closeAll(); } },
  { label: '关于', icon: IconInfoCircle, action: () => { closeMenus(); busyInfo.value = true; } },
];

// ---- 窗口内容屏蔽浏览器右键菜单（输入框/文本域除外，保留粘贴菜单）----

function onGlobalContextMenu(e) {
  const t = e.target;
  if (!t.closest || !t.closest('.win')) return;                 // 桌面/Dock 有自己的菜单逻辑
  if (t.matches('input, textarea') || t.isContentEditable) return; // 保留系统粘贴菜单
  e.preventDefault();
}

// ---- 桌面壁纸样式 ----

const desktopStyle = computed(() => {
  const w = wallpaper.state;
  let style;
  if (w.custom) {
    // url() 必须带引号：dataURL 里可能出现未编码的括号/引号字符（如 SVG 的 url(#id)），
    // 不加引号整条 background 声明会被浏览器静默丢弃
    style = { backgroundImage: `url("${w.custom}")`, backgroundSize: 'cover', backgroundPosition: 'center' };
  } else {
    const preset = wallpaper.presets.find(p => p.id === w.presetId) || wallpaper.presets[0];
    style = { backgroundImage: preset.css };
    if (preset.size) style.backgroundSize = preset.size;
  }
  // 自定义壁纸取色 → 覆写 Dock 配色。CSS 变量沿 .desktop 继承进 Dock 组件，
  // Dock 自身零改动；色相来自壁纸，明度/饱和度已在 useWallpaper 压进深色玻璃区间
  const t = w.tint;
  if (t) {
    style['--dock-bg'] = `hsla(${t.h}, ${(t.s * 100).toFixed(1)}%, ${(t.l * 100).toFixed(1)}%, 0.60)`;
    style['--dock-border'] = `hsla(${t.h}, ${Math.min(50, t.s * 100).toFixed(1)}%, 68%, 0.20)`;
  }
  return style;
});

/** 光柱/地面网格只在 CSS 渐变壁纸上叠加；自定义图片壁纸时只留粒子。 */
const decorative = computed(() => !wallpaper.state.custom);

function windowFor(win) {
  return win.appDef || apps.find(a => a.id === win.appId);
}

onMounted(() => document.addEventListener('contextmenu', onGlobalContextMenu, true));
onUnmounted(() => document.removeEventListener('contextmenu', onGlobalContextMenu, true));
</script>

<template>
  <div class="desktop" :style="desktopStyle" @click="closeMenus" @contextmenu="onDesktopContextMenu">
    <ParticleFlow v-if="particlesOn" :decorative="decorative" />

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
      <button type="button" @click="toggleParticles">
        <IconSparkles /> 粒子效果
        <span class="ctx-state" :class="{ on: particlesOn }">{{ particlesOn ? '已开启' : '已关闭' }}</span>
      </button>
      <button type="button" :disabled="busyInfo" @click="busyInfo = true; closeMenus()"><IconInfoCircle /> 关于</button>
    </div>

    <!-- Dock（任务栏）右键快捷菜单：与桌面菜单同款造型、实例独立 -->
    <div
      v-if="dockMenu"
      class="ctx-menu"
      :style="{ left: dockMenu.x + 'px', top: dockMenu.y + 'px' }"
      @click.stop
      @contextmenu.prevent
    >
      <button
        v-for="mi in dockMenuItems"
        :key="mi.label"
        type="button"
        :class="{ danger: mi.danger }"
        @click="mi.action()"
      >
        <component :is="mi.icon" /> {{ mi.label }}
      </button>
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

    <TopBar
      :apps="apps"
      :windows="windows"
      :steam-widget-open="steamWidgetOpen"
      @focus-window="toggleFocus"
      @about="busyInfo = true"
      @change-wallpaper="changeWallpaper"
      @reset-wallpaper="resetWallpaper"
      @toggle-widget="toggleWidget"
      @launch="launchApp"
    />

    <Dock
      :apps="apps"
      :windows="windows"
      :widgets="widgets"
      :open-widgets="steamWidgetOpen ? ['steam'] : []"
      @launch="launchApp"
      @focus-window="toggleFocus"
      @toggle-widget="toggleWidget"
    />

    <SteamWidget v-if="steamWidgetOpen" @close="steamWidgetOpen = false" />

    <CropperHost />

    <div v-if="busyInfo" class="about-mask" @click.self="busyInfo = false" @contextmenu.prevent>
      <div class="about-box">
        <div class="about-title">DT_Tools WebUI</div>
        <div class="about-line">Deadly Trick 游戏工具插件</div>
        <div class="about-line">所有配置 / 重命名 / 壁纸 / 图标仅存于本浏览器。</div>
        <div class="about-refs">
          <div class="about-refs-title">主题参考：</div>
          <a href="https://0l1v3rr.github.io/" target="_blank" rel="noopener noreferrer">https://0l1v3rr.github.io/</a>
          <a href="https://os.inori.ai/" target="_blank" rel="noopener noreferrer">https://os.inori.ai/</a>
        </div>
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
.ctx-menu button svg { width: 14px; height: 14px; color: var(--shell-glow); flex-shrink: 0; }
.ctx-menu button:hover { background: rgba(150, 225, 255, 0.10); color: var(--text-0); }
.ctx-menu button:disabled { opacity: 0.5; cursor: default; }
/* 开关类菜单项右端的状态角标（同顶栏面板 km-item-state 的语义） */
.ctx-state { margin-left: auto; padding-left: 14px; font-size: 10.5px; color: var(--text-2); }
.ctx-state.on { color: var(--accent-green); }
/* 破坏性操作（关闭所有进程）：红色可辨 */
.ctx-menu button.danger { color: var(--accent-red); }
.ctx-menu button.danger svg { color: var(--accent-red); }
.ctx-menu button.danger:hover { background: rgba(236, 1, 1, 0.12); color: var(--accent-red); }

.about-mask {
  position: fixed;
  inset: 0;
  z-index: 10002;
  background: rgba(0, 0, 0, 0.5);
  display: grid;
  place-items: center;
}
.about-box {
  width: 360px;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--win-shadow);
  padding: 16px;
  text-align: center;
}
.about-title { font-size: 15px; font-weight: 700; color: var(--shell-glow); font-family: var(--font-mono); margin-bottom: 8px; }
.about-line { font-size: 12px; color: var(--text-1); line-height: 1.7; }
.about-refs { margin-top: 10px; padding-top: 10px; border-top: 1px solid var(--line); }
.about-refs-title { font-size: 12px; color: var(--text-1); margin-bottom: 4px; }
.about-refs a {
  display: block;
  font-family: var(--font-mono);
  font-size: 11.5px;
  color: var(--shell-glow);
  text-decoration: none;
  line-height: 1.8;
}
.about-refs a:hover { text-decoration: underline; }
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
