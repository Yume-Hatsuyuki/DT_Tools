<script setup>
import { t } from '../i18n/index.js';
import LanguageModal from './LanguageModal.vue';
import IconSettings from '~icons/tabler/settings';
import { ref, markRaw, computed, onMounted, onUnmounted } from 'vue';
import TopBar from './TopBar.vue';
import Dock from './Dock.vue';
import ParticleFlow from './ParticleFlow.vue';
import SteamWidget from './SteamWidget.vue';
import McpWidget from './McpWidget.vue';
import NewsWidget from './NewsWidget.vue';
import Window from './Window.vue';
import UpdateModal from './UpdateModal.vue';
import CropperHost from '../apps/common/CropperHost.vue';
import { useWindowManager } from '../composables/useWindowManager.js';
import { pickImageFile } from '../composables/pickImage.js';
import { useWallpaper } from '../composables/useWallpaper.js';
import { useCropper } from '../composables/useCropper.js';
import { useUpdateCheck } from '../composables/useUpdateCheck.js';
import { useLogStream } from '../composables/useLogStream.js';
import { API } from '../api.js';

import ConsoleApp from '../apps/console/ConsoleApp.vue';
import TerminalApp from '../apps/console/TerminalApp.vue';
import ConfigApp from '../apps/config/ConfigApp.vue';
import AutomationApp from '../apps/automation/AutomationApp.vue';
import LogApp from '../apps/log/LogApp.vue';
import DummyApp from '../apps/dummy/DummyApp.vue';
import Mp3App from '../apps/mp3/Mp3App.vue';
import GamepadApp from '../apps/gamepad/GamepadApp.vue';
import TabletApp from '../apps/tablet/TabletApp.vue';

import IconTerminal from '~icons/tabler/terminal';
import IconPhoto from '~icons/tabler/photo';
import IconPhotoOff from '~icons/tabler/photo-off';
import IconInfoCircle from '~icons/tabler/info-circle';
import IconRefresh from '~icons/tabler/refresh';
import IconSparkles from '~icons/tabler/sparkles';
import IconArrowBarToDown from '~icons/tabler/arrow-bar-to-down';
import IconSquareX from '~icons/tabler/square-x';
import IconLayoutBottombarCollapse from '~icons/tabler/layout-bottombar-collapse';
import IconDragon from '~icons/tabler/dragon';
import IconTerminal2 from '~icons/tabler/terminal-2';
import IconAdjustmentsHorizontal from '~icons/tabler/adjustments-horizontal';
import IconRobot from '~icons/tabler/robot';
import IconListDetails from '~icons/tabler/list-details';
import IconUsersGroup from '~icons/tabler/users-group';
import IconDeviceAudioTape from '~icons/tabler/device-audio-tape';
import IconDeviceGamepad2 from '~icons/tabler/device-gamepad-2';
import IconDeviceTablet from '~icons/tabler/device-tablet';
import IconBrandSteam from '~icons/tabler/brand-steam';
import IconNews from '~icons/tabler/news';
import IconModelAi from '~icons/tabler/model-ai';

const apps = [
  { id: 'terminal', get title() { return t('app.terminal'); }, icon: markRaw(IconDragon), component: markRaw(TerminalApp), defaults: { x: 60, y: 50, w: 760, h: 520 } },
  { id: 'console', get title() { return t('app.console'); }, icon: markRaw(IconTerminal2), component: markRaw(ConsoleApp), defaults: { x: 90, y: 80, w: 760, h: 520 } },
  { id: 'config', get title() { return t('app.config'); }, icon: markRaw(IconAdjustmentsHorizontal), component: markRaw(ConfigApp), defaults: { x: 140, y: 110, w: 860, h: 580 } },
  { id: 'automation', get title() { return t('app.automation'); }, icon: markRaw(IconRobot), component: markRaw(AutomationApp), defaults: { x: 180, y: 140, w: 720, h: 520 } },
  { id: 'dummy', get title() { return t('app.dummy'); }, icon: markRaw(IconUsersGroup), component: markRaw(DummyApp), defaults: { x: 220, y: 170, w: 780, h: 480 } },
  { id: 'mp3', get title() { return t('app.mp3'); }, icon: markRaw(IconDeviceAudioTape), component: markRaw(Mp3App), defaults: { x: 300, y: 230, w: 620, h: 560 }, onClose: () => API.mp3Stop() },
  { id: 'gamepad', get title() { return t('app.gamepad'); }, icon: markRaw(IconDeviceGamepad2), component: markRaw(GamepadApp), defaults: { x: 340, y: 260, w: 720, h: 580 } },
  { id: 'tablet', get title() { return t('app.tablet'); }, icon: markRaw(IconDeviceTablet), component: markRaw(TabletApp), defaults: { x: 240, y: 150, w: 980, h: 620 } },
  { id: 'log', get title() { return t('app.log'); }, icon: markRaw(IconListDetails), component: markRaw(LogApp), defaults: { x: 260, y: 200, w: 820, h: 560 } },
];

useLogStream();

const upd = useUpdateCheck();
upd.start();

const { windows, open, close, focus, minimize, toggleFocus, toggleMaximize, updateGeometry, showDesktop, minimizeAll, closeAll } = useWindowManager();
const wallpaper = useWallpaper();
const cropper = useCropper();

const TILE_KEY = 'dt_desktop_tile_v1';
const tileMode = ref(localStorage.getItem(TILE_KEY) === '1');
function toggleTileMode() {
  tileMode.value = !tileMode.value;
  localStorage.setItem(TILE_KEY, tileMode.value ? '1' : '0');
}
const tiledActiveId = computed(() => {
  if (!tileMode.value) return null;
  const top = windows.filter(w => !w.minimized).reduce((a, b) => (!a || b.z > a.z ? b : a), null);
  return top ? top.id : null;
});

const desktopMenu = ref(null);
const dockMenu = ref(null);
const busyInfo = ref(false);
const showUpdate = ref(false);
const showLanguage = ref(false);

const WIDGET_STORE_KEY = 'dt_steam_widget_v1';
const MCP_STORE_KEY = 'dt_mcp_widget_v1';
const NEWS_STORE_KEY = 'dt_news_widget_v1';
function widgetClosedAtStart(key) {
  try { return !!(JSON.parse(localStorage.getItem(key)) || {}).closed; }
  catch { return false; }
}
const steamWidgetOpen = ref(!widgetClosedAtStart(WIDGET_STORE_KEY));
const mcpWidgetOpen = ref(!widgetClosedAtStart(MCP_STORE_KEY));
const newsWidgetOpen = ref(!widgetClosedAtStart(NEWS_STORE_KEY));

const widgets = [
  { id: 'steam', get title() { return t('widget.steam'); }, icon: markRaw(IconBrandSteam) },
  { id: 'mcp', get title() { return t('widget.mcp'); }, icon: markRaw(IconModelAi) },
  { id: 'news', get title() { return t('widget.news'); }, icon: markRaw(IconNews) },
];

function toggleWidget(id) {
  if (id === 'steam') steamWidgetOpen.value = !steamWidgetOpen.value;
  else if (id === 'mcp') mcpWidgetOpen.value = !mcpWidgetOpen.value;
  else if (id === 'news') newsWidgetOpen.value = !newsWidgetOpen.value;
}

const openWidgets = computed(() => [
  ...(steamWidgetOpen.value ? ['steam'] : []),
  ...(mcpWidgetOpen.value ? ['mcp'] : []),
  ...(newsWidgetOpen.value ? ['news'] : []),
]);

function launchApp(appId) {
  const app = apps.find(a => a.id === appId);
  if (!app) return;
  const win = open(appId, { title: app.title, icon: app.icon, defaults: app.defaults });
  win.appDef = markRaw(app);
}

function onWindowClose(win) {
  try { windowFor(win)?.onClose?.(); } catch { }
  close(win.id);
}

function closeAllWindows() {
  windows.forEach(w => { try { windowFor(w)?.onClose?.(); } catch { } });
  closeAll();
}

function onDesktopContextMenu(e) {
  if (e.target.closest && e.target.closest('.win')) return;
  e.preventDefault();
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
  const dataUrl = await cropper.open(file, {
    title: t('crop.wallpaper'),
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

const PARTICLES_KEY = 'dt_particles_v1';
const particlesOn = ref((() => {
  try { return localStorage.getItem(PARTICLES_KEY) !== '0'; } catch { return true; }
})());
function toggleParticles() {
  particlesOn.value = !particlesOn.value;
  try { localStorage.setItem(PARTICLES_KEY, particlesOn.value ? '1' : '0'); } catch { }
  closeMenus();
}

const dockMenuItems = [
  { get label() { return t('menu.openTerminal'); }, icon: IconTerminal, action: () => { closeMenus(); launchApp('terminal'); } },
  { get label() { return t('menu.showDesktop'); }, icon: IconLayoutBottombarCollapse, action: () => { closeMenus(); showDesktop(); } },
  { get label() { return t('menu.minimizeAll'); }, icon: IconArrowBarToDown, action: () => { closeMenus(); minimizeAll(); } },
  { get label() { return t('menu.closeAll'); }, icon: IconSquareX, danger: true, action: () => { closeMenus(); closeAllWindows(); } },
  { get label() { return t('common.about'); }, icon: IconInfoCircle, action: () => { closeMenus(); busyInfo.value = true; } },
];

function onGlobalContextMenu(e) {
  const t = e.target;
  if (!t.closest || !t.closest('.win')) return;
  if (t.matches('input, textarea') || t.isContentEditable) return;
  e.preventDefault();
}

const desktopStyle = computed(() => {
  const w = wallpaper.state;
  let style;
  if (w.custom) {
    style = { backgroundImage: `url("${w.custom}")`, backgroundSize: 'cover', backgroundPosition: 'center' };
  } else {
    const preset = wallpaper.presets.find(p => p.id === w.presetId) || wallpaper.presets[0];
    style = { backgroundImage: preset.css };
    if (preset.size) style.backgroundSize = preset.size;
  }
  const t = w.tint;
  if (t) {
    style['--dock-bg'] = `hsla(${t.h}, ${(t.s * 100).toFixed(1)}%, ${(t.l * 100).toFixed(1)}%, 0.60)`;
    style['--dock-border'] = `hsla(${t.h}, ${Math.min(50, t.s * 100).toFixed(1)}%, 68%, 0.20)`;
  }
  return style;
});

const decorative = computed(() => !wallpaper.state.custom);

function windowFor(win) {
  return win.appDef || apps.find(a => a.id === win.appId);
}

onMounted(() => document.addEventListener('contextmenu', onGlobalContextMenu, true));
onUnmounted(() => document.removeEventListener('contextmenu', onGlobalContextMenu, true));
</script>

<template>
  <div class="desktop" :style="desktopStyle" @click="closeMenus" @contextmenu="onDesktopContextMenu">
    <ParticleFlow v-if="particlesOn && !tileMode" :decorative="decorative" />

    <div
      v-if="desktopMenu"
      class="ctx-menu"
      :style="{ left: desktopMenu.x + 'px', top: desktopMenu.y + 'px' }"
      @click.stop
      @contextmenu.prevent
    >
      <button type="button" @click="launchApp('terminal'); closeMenus()"><IconTerminal /> {{ t('menu.openTerminal') }}</button>
      <button type="button" @click="changeWallpaper"><IconPhoto /> {{ t('menu.wallpaper') }}</button>
      <button type="button" @click="resetWallpaper"><IconPhotoOff /> {{ t('menu.resetWallpaper') }}</button>
      <button type="button" @click="toggleParticles">
        <IconSparkles /> {{ t('menu.particles') }}
        <span class="ctx-state" :class="{ on: particlesOn }">{{ particlesOn ? t('state.on') : t('state.off') }}</span>
      </button>
      <button type="button" :disabled="busyInfo" @click="busyInfo = true; closeMenus()"><IconInfoCircle /> {{ t('common.about') }}</button>
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
      :tiled="tileMode"
      :active="win.id === tiledActiveId"
      @close="onWindowClose(win)"
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
      :mcp-widget-open="mcpWidgetOpen"
      :tile-mode="tileMode"
      @toggle-tile="toggleTileMode"
      @focus-window="toggleFocus"
      @about="busyInfo = true"
      @open-language="showLanguage = true"
      @show-update="showUpdate = true"
      @change-wallpaper="changeWallpaper"
      @reset-wallpaper="resetWallpaper"
      @toggle-widget="toggleWidget"
      @launch="launchApp"
    />

    <Dock
      :apps="apps"
      :windows="windows"
      :widgets="widgets"
      :open-widgets="openWidgets"
      @launch="launchApp"
      @focus-window="toggleFocus"
      @toggle-widget="toggleWidget"
    />

    <SteamWidget v-if="steamWidgetOpen" @close="steamWidgetOpen = false" />
    <McpWidget v-if="mcpWidgetOpen" @close="mcpWidgetOpen = false" />
    <NewsWidget v-if="newsWidgetOpen" @close="newsWidgetOpen = false" />
    <LanguageModal v-if="showLanguage" @close="showLanguage = false" />

    <CropperHost />

    <div v-if="busyInfo" class="about-mask" @click.self="busyInfo = false" @contextmenu.prevent>
      <div class="about-box">
        <div class="about-title">DT_Tools WebUI</div>
        <div class="about-line">{{ t('about.product') }}</div>
        <div class="about-line mono">{{ t('about.version', { v: upd.state.current || '…' }) }}</div>

        <!-- 检查更新：手动触发（绕过后端缓存），结果直接显示在关于框内 -->
        <div class="about-check-row">
          <button type="button" class="about-check" :disabled="upd.state.checking" @click="upd.manualCheck()">
            <IconRefresh :class="{ spin: upd.state.checking }" />
            {{ upd.state.checking ? t('common.checking') : t('about.check') }}
          </button>
        </div>
        <div v-if="!upd.state.checking && upd.state.error" class="about-upd err">{{ t('about.checkFailed', { msg: upd.state.error }) }}</div>
        <div v-else-if="!upd.state.checking && upd.state.hasUpdate" class="about-upd new">
          {{ t('about.newVersion', { tag: upd.state.latest && upd.state.latest.tag }) }}
          <button type="button" class="about-upd-link" @click="busyInfo = false; showUpdate = true">{{ t('about.viewDetails') }}</button>
        </div>
        <div v-else-if="!upd.state.checking && upd.state.lastChecked" class="about-upd ok">{{ t('about.upToDate') }}</div>

        <div class="about-line">{{ t('about.storage') }}</div>
        <div class="about-refs">
          <div class="about-refs-title">{{ t('about.themeRefs') }}</div>
          <a href="https://0l1v3rr.github.io/" target="_blank" rel="noopener noreferrer">https://0l1v3rr.github.io/</a>
          <a href="https://os.inori.ai/" target="_blank" rel="noopener noreferrer">https://os.inori.ai/</a>
        </div>
        <button type="button" class="about-close" @click="busyInfo = false">{{ t('common.close') }}</button>
      </div>
    </div>

    <!-- 软件更新弹窗：新版本信息 + 更新说明 + 下载（顶栏徽标/气泡与关于弹窗共用） -->
    <UpdateModal v-if="showUpdate" @close="showUpdate = false" />
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
.ctx-state { margin-left: auto; padding-left: 14px; font-size: 10.5px; color: var(--text-2); }
.ctx-state.on { color: var(--accent-green); }
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
.about-line.mono { font-family: var(--font-mono); color: var(--text-0); }

.about-check-row { display: flex; justify-content: center; margin-top: 10px; }
.about-check {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: rgba(150, 225, 255, 0.08);
  border: 1px solid rgba(150, 225, 255, 0.18);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 5px 14px;
  cursor: pointer;
}
.about-check svg { width: 13px; height: 13px; color: var(--shell-glow); }
.about-check:hover { background: rgba(150, 225, 255, 0.14); color: var(--text-0); }
.about-check:disabled { opacity: 0.5; cursor: default; }
.spin { animation: about-spin 1s linear infinite; }
@keyframes about-spin { to { transform: rotate(360deg); } }

.about-upd { margin-top: 8px; font-size: 12px; }
.about-upd.err { color: var(--accent-red); }
.about-upd.ok { color: var(--accent-green); }
.about-upd.new { color: var(--accent-green); display: flex; align-items: center; justify-content: center; gap: 8px; }
.about-upd-link {
  background: rgba(71, 212, 185, 0.12);
  border: 1px solid rgba(71, 212, 185, 0.4);
  border-radius: var(--radius-sm);
  color: var(--accent-green);
  font-size: 11.5px;
  padding: 2px 8px;
  cursor: pointer;
}
.about-upd-link:hover { background: rgba(71, 212, 185, 0.22); }
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
