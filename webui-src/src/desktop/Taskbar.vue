<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useSteamPlayers } from '../composables/useSteamPlayers.js';
import { useConnectionStatus } from '../composables/useConnectionStatus.js';
import IconLayoutGrid from '~icons/tabler/layout-grid';

const props = defineProps({
  windows: { type: Array, required: true },
  apps: { type: Array, required: true },
});
const emit = defineEmits(['focus-window', 'launch']);

const { count: steamCount, ok: steamOk } = useSteamPlayers();
const conn = useConnectionStatus();

const now = ref(new Date());
let timer = null;
onMounted(() => { timer = setInterval(() => { now.value = new Date(); }, 1000 * 15); });
onUnmounted(() => clearInterval(timer));

const clock = computed(() =>
  now.value.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
);

const launcherOpen = ref(false);
function toggleLauncher() { launcherOpen.value = !launcherOpen.value; }
function launch(appId) {
  launcherOpen.value = false;
  emit('launch', appId);
}
</script>

<template>
  <div class="taskbar">
    <button class="launcher-btn" :class="{ active: launcherOpen }" @click="toggleLauncher" title="所有应用">
      <IconLayoutGrid />
    </button>

    <div v-if="launcherOpen" class="launcher-menu" @click.self="launcherOpen = false">
      <button v-for="app in apps" :key="app.id" class="launcher-item" @click="launch(app.id)">
        <component :is="app.icon" class="launcher-item-icon" />
        <span>{{ app.title }}</span>
      </button>
    </div>

    <div class="taskbar-running">
      <button
        v-for="w in windows"
        :key="w.id"
        class="taskbar-tab"
        :class="{ minimized: w.minimized }"
        @click="emit('focus-window', w.id)"
      >
        <component :is="w.icon" v-if="w.icon" class="taskbar-tab-icon" />
        {{ w.title }}
      </button>
    </div>

    <div class="taskbar-tray">
      <div class="tray-item steam" :class="{ ok: steamOk }" title="Steam 全球当前在线玩家数（每分钟刷新）">
        <span class="pulse" />
        <span class="tray-label">《Deadly Trick》在线</span>
        <span class="tray-value">{{ steamOk && steamCount != null ? steamCount.toLocaleString('en-US') : '--' }}</span>
      </div>
      <div class="tray-item conn" :class="{ ok: conn.online }">
        <span class="dot" />
        {{ conn.online ? '已连接' : '连接中…' }}
      </div>
      <div class="tray-item clock">{{ clock }}</div>
    </div>
  </div>
</template>

<style scoped>
.taskbar {
  position: fixed;
  left: 0; right: 0; bottom: 0;
  height: 48px;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 10px;
  background: var(--taskbar-bg);
  border-top: 1px solid var(--taskbar-border);
  backdrop-filter: blur(20px) saturate(150%);
  z-index: 5000;
}

.launcher-btn {
  width: 36px; height: 36px;
  display: grid; place-items: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  color: var(--accent-cyan);
  cursor: pointer;
}
.launcher-btn svg { width: 19px; height: 19px; }
.launcher-btn:hover, .launcher-btn.active {
  background: rgba(0, 229, 255, 0.1);
  border-color: rgba(0, 229, 255, 0.25);
}

.launcher-menu {
  position: fixed;
  left: 8px;
  bottom: 56px;
  width: 240px;
  max-height: 60vh;
  overflow-y: auto;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--win-radius);
  box-shadow: var(--win-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.launcher-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 10px;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 13px;
  text-align: left;
  cursor: pointer;
}
.launcher-item:hover { background: var(--surface-2); color: var(--text-0); }
.launcher-item-icon { width: 17px; height: 17px; color: var(--accent-cyan); flex-shrink: 0; }

.taskbar-running {
  flex: 1;
  display: flex;
  gap: 6px;
  overflow-x: auto;
  min-width: 0;
}
.taskbar-tab {
  display: flex;
  align-items: center;
  gap: 7px;
  height: 34px;
  padding: 0 12px;
  background: var(--surface-1);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12.5px;
  white-space: nowrap;
  cursor: pointer;
  flex-shrink: 0;
}
.taskbar-tab:hover { color: var(--text-0); border-color: var(--line-strong); }
.taskbar-tab.minimized { opacity: 0.55; }
.taskbar-tab-icon { width: 14px; height: 14px; color: var(--accent-cyan); }

.taskbar-tray {
  display: flex;
  align-items: center;
  gap: 14px;
  flex-shrink: 0;
  font-size: 12px;
  color: var(--text-2);
}
.tray-item { display: flex; align-items: center; gap: 6px; }

.tray-item.steam .pulse {
  width: 6px; height: 6px; border-radius: 50%;
  background: var(--text-2);
}
.tray-item.steam.ok .pulse {
  background: var(--accent-green);
  box-shadow: 0 0 8px rgba(61, 255, 154, 0.5);
}
.tray-item.steam.ok .tray-value { color: var(--accent-green); font-weight: 600; }
.tray-label { display: none; }
@media (min-width: 1100px) { .tray-label { display: inline; } }

.tray-item.conn .dot {
  width: 6px; height: 6px; border-radius: 50%;
  background: var(--accent-red);
}
.tray-item.conn.ok .dot { background: var(--accent-green); box-shadow: 0 0 8px rgba(61, 255, 154, 0.5); }

.tray-item.clock {
  font-family: var(--font-mono);
  color: var(--text-1);
  min-width: 42px;
}
</style>
