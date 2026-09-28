<script setup>
import { ref, markRaw } from 'vue';
import Taskbar from './Taskbar.vue';
import DesktopIcon from './DesktopIcon.vue';
import Window from './Window.vue';
import { useWindowManager } from '../composables/useWindowManager.js';
import { useCustomIcons } from '../composables/useCustomIcons.js';

import ConsoleApp from '../apps/console/ConsoleApp.vue';
import ConfigApp from '../apps/config/ConfigApp.vue';
import AutomationApp from '../apps/automation/AutomationApp.vue';

import IconTerminal2 from '~icons/tabler/terminal-2';
import IconAdjustmentsHorizontal from '~icons/tabler/adjustments-horizontal';
import IconRobot from '~icons/tabler/robot';

const apps = [
  { id: 'console', title: 'DT 控制台', icon: markRaw(IconTerminal2), component: markRaw(ConsoleApp), defaults: { x: 60, y: 50, w: 760, h: 520 } },
  { id: 'config', title: 'DT 配置', icon: markRaw(IconAdjustmentsHorizontal), component: markRaw(ConfigApp), defaults: { x: 140, y: 90, w: 860, h: 580 } },
  { id: 'automation', title: '自动化', icon: markRaw(IconRobot), component: markRaw(AutomationApp), defaults: { x: 220, y: 130, w: 720, h: 520 } },
];

const { windows, open, close, focus, minimize, toggleMaximize, updateGeometry } = useWindowManager();
const { get: getCustomIcon, pickAndSet, clear: clearCustomIcon } = useCustomIcons();

const selectedIcon = ref(null);
const ctxMenu = ref(null);   // { x, y, appId }

function launchApp(appId) {
  const app = apps.find(a => a.id === appId);
  if (!app) return;
  const win = open(appId, { title: app.title, icon: app.icon, defaults: app.defaults });
  win.appDef = markRaw(app);
}

function onIconContextMenu(e, appId) {
  e.preventDefault();
  selectedIcon.value = appId;
  ctxMenu.value = { x: e.clientX, y: e.clientY, appId };
}
function closeCtxMenu() { ctxMenu.value = null; }

async function importIcon(appId) {
  closeCtxMenu();
  const result = await pickAndSet(appId);
  if (result && result.error) {
    // 简单起见用系统 alert；桌面壳的轻量提示后续可以换成 toast
    alert(result.error);
  }
}
function resetIcon(appId) {
  clearCustomIcon(appId);
  closeCtxMenu();
}

function windowFor(win) {
  return apps.find(a => a.id === win.appId);
}
</script>

<template>
  <div class="desktop" @click="closeCtxMenu(); selectedIcon = null" @contextmenu.self.prevent>
    <div class="desktop-grid">
      <DesktopIcon
        v-for="app in apps"
        :key="app.id"
        :label="app.title"
        :icon="app.icon"
        :custom-src="getCustomIcon(app.id)"
        :selected="selectedIcon === app.id"
        @select.stop="selectedIcon = app.id"
        @open="launchApp(app.id)"
        @contextmenu.prevent.stop="onIconContextMenu($event, app.id)"
      />
    </div>

    <div
      v-if="ctxMenu"
      class="icon-ctx-menu"
      :style="{ left: ctxMenu.x + 'px', top: ctxMenu.y + 'px' }"
      @click.stop
    >
      <button @click="launchApp(ctxMenu.appId); closeCtxMenu()">打开</button>
      <button @click="importIcon(ctxMenu.appId)">导入自定义图标…</button>
      <button v-if="getCustomIcon(ctxMenu.appId)" @click="resetIcon(ctxMenu.appId)">恢复默认图标</button>
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

    <Taskbar :windows="windows" :apps="apps" @focus-window="focus" @launch="launchApp" />
  </div>
</template>

<style scoped>
.desktop {
  position: fixed;
  inset: 0;
  background:
    radial-gradient(1400px 700px at 8% -10%, rgba(0, 229, 255, 0.05), transparent 55%),
    radial-gradient(1000px 600px at 100% 0%, rgba(255, 45, 149, 0.04), transparent 50%),
    var(--desktop-bg-0);
  background-image:
    linear-gradient(var(--desktop-grid-line) 1px, transparent 1px),
    linear-gradient(90deg, var(--desktop-grid-line) 1px, transparent 1px),
    radial-gradient(1400px 700px at 8% -10%, rgba(0, 229, 255, 0.05), transparent 55%),
    radial-gradient(1000px 600px at 100% 0%, rgba(255, 45, 149, 0.04), transparent 50%),
    linear-gradient(var(--desktop-bg-1), var(--desktop-bg-0));
  background-size: 42px 42px, 42px 42px, auto, auto, auto;
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

.icon-ctx-menu {
  position: fixed;
  z-index: 9999;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-sm);
  box-shadow: var(--win-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  padding: 4px;
  display: flex;
  flex-direction: column;
  min-width: 170px;
}
.icon-ctx-menu button {
  background: transparent;
  border: none;
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  cursor: pointer;
}
.icon-ctx-menu button:hover { background: var(--surface-2); color: var(--text-0); }
</style>
