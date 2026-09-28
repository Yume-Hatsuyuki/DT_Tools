<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useSteamPlayers } from '../composables/useSteamPlayers.js';
import { useConnectionStatus } from '../composables/useConnectionStatus.js';
import { useShellUser } from '../composables/useShellUser.js';
import { pickImageFile } from '../composables/pickImage.js';
import { useCropper } from '../composables/useCropper.js';
import { API } from '../api.js';
import IconLayoutGrid from '~icons/tabler/layout-grid';
import IconUser from '~icons/tabler/user';
import IconUserEdit from '~icons/tabler/user-edit';
import IconPhoto from '~icons/tabler/photo';
import IconPhotoOff from '~icons/tabler/photo-off';
import IconPower from '~icons/tabler/power';

const props = defineProps({
  windows: { type: Array, required: true },
  apps: { type: Array, required: true },
});
const emit = defineEmits(['focus-window', 'launch']);

const { count: steamCount, ok: steamOk } = useSteamPlayers();
const conn = useConnectionStatus();
const shell = useShellUser();
const cropper = useCropper();

// 时：分：秒，1s 刷新
const now = ref(new Date());
let timer = null;
onMounted(() => { timer = setInterval(() => { now.value = new Date(); }, 1000); });
onUnmounted(() => clearInterval(timer));

const clock = computed(() => {
  const d = now.value;
  const p = (n) => String(n).padStart(2, '0');
  return `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
});

// ---- 开始菜单（用户配置整合在头部用户位，Windows 风格）----
const launcherOpen = ref(false);
const userCfgOpen = ref(false);

function toggleLauncher() {
  launcherOpen.value = !launcherOpen.value;
  if (!launcherOpen.value) userCfgOpen.value = false;
}
function launch(appId) {
  launcherOpen.value = false;
  userCfgOpen.value = false;
  emit('launch', appId);
}

function toggleUserCfg() { userCfgOpen.value = !userCfgOpen.value; }

async function renameUser() {
  userCfgOpen.value = false;
  const name = prompt('修改用户名（终端提示符同步更新）：', shell.state.user);
  if (name === null) return;
  const r = shell.setUser(name);
  if (!r.ok) alert(r.error);
}

async function changeAvatar() {
  userCfgOpen.value = false;
  const file = await pickImageFile();
  if (!file) return;
  const dataUrl = await cropper.open(file, { title: '裁切头像', shape: 'circle' });
  if (dataUrl) shell.setAvatar(dataUrl);
}
function resetAvatar() {
  userCfgOpen.value = false;
  shell.setAvatar(null);
}

async function exitGame() {
  launcherOpen.value = false;
  userCfgOpen.value = false;
  if (!confirm('确定退出游戏？未保存的配置将丢失（可在退出前先点"保存"）。')) return;
  const j = await API.gameExit();
  if (!j || !j.ok) {
    alert('退出指令发送失败：' + ((j && j.error) || '未知错误'));
  }
}

function closeMenus() {
  launcherOpen.value = false;
  userCfgOpen.value = false;
}
</script>

<template>
  <div class="taskbar" @click="closeMenus">
    <button class="start-btn" :class="{ active: launcherOpen }" title="所有应用" @click.stop="toggleLauncher">
      <IconLayoutGrid />
    </button>

    <div v-if="launcherOpen" class="start-menu" @click.stop>
      <!-- 用户位：头像 + user@hostname，点击展开账户操作（Windows 开始菜单风格） -->
      <button class="user-tile" :class="{ open: userCfgOpen }" @click="toggleUserCfg">
        <img v-if="shell.state.avatar" :src="shell.state.avatar" alt="" class="user-tile-avatar">
        <IconUser v-else class="user-tile-avatar-fallback" />
        <span class="user-tile-name">{{ shell.state.user }}@{{ shell.state.hostname }}</span>
      </button>
      <div v-if="userCfgOpen" class="user-cfg">
        <button @click="renameUser"><IconUserEdit /> 修改用户名…</button>
        <button @click="changeAvatar"><IconPhoto /> 更换头像…</button>
        <button v-if="shell.state.avatar" @click="resetAvatar"><IconPhotoOff /> 恢复默认头像</button>
      </div>

      <div class="menu-sep" />
      <button v-for="app in apps" :key="app.id" class="start-item" @click="launch(app.id)">
        <component :is="app.icon" class="start-item-icon" />
        <span>{{ app.title }}</span>
      </button>

      <div class="menu-sep" />
      <button class="start-item exit-item" @click="exitGame">
        <IconPower class="exit-icon" />
        <span>退出游戏</span>
      </button>
    </div>

    <div class="taskbar-running">
      <button
        v-for="w in windows"
        :key="w.id"
        class="taskbar-tab"
        :class="{ minimized: w.minimized }"
        :title="w.title"
        @click.stop="emit('focus-window', w.id)"
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
  box-shadow: 0 -8px 24px rgba(4, 10, 28, 0.5);
  backdrop-filter: blur(20px) saturate(150%);
  z-index: 5000;
}

.start-btn {
  width: 36px;
  height: 36px;
  display: grid; place-items: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  color: var(--accent-cyan);
  cursor: pointer;
  flex-shrink: 0;
}
.start-btn svg { width: 19px; height: 19px; }
.start-btn:hover, .start-btn.active {
  background: rgba(39, 127, 255, 0.12);
  border-color: rgba(39, 127, 255, 0.35);
}

.start-menu {
  position: fixed;
  left: 8px;
  bottom: 56px;
  width: 260px;
  max-height: 70vh;
  overflow-y: auto;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--win-radius);
  box-shadow: var(--menu-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  z-index: 6000;
}

/* 用户位（原灰色文字表头升级为 Windows 式账户入口） */
.user-tile {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  cursor: pointer;
  text-align: left;
}
.user-tile:hover, .user-tile.open { background: var(--surface-2); }
.user-tile.open { border-color: var(--line); }
.user-tile-avatar {
  width: 30px; height: 30px;
  border-radius: 50%;
  object-fit: cover;
  border: 1px solid var(--line-strong);
  flex-shrink: 0;
}
.user-tile-avatar-fallback {
  width: 24px; height: 24px;
  color: var(--accent-cyan);
  flex-shrink: 0;
  padding: 3px;
  border: 1px solid var(--line-strong);
  border-radius: 50%;
}
.user-tile-name {
  font-family: var(--font-mono);
  font-size: 12.5px;
  color: var(--text-0);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-cfg { display: flex; flex-direction: column; gap: 2px; padding: 4px 0 6px; }
.user-cfg button {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  cursor: pointer;
}
.user-cfg button:hover { background: var(--surface-2); color: var(--text-0); }
.user-cfg button svg { width: 15px; height: 15px; color: var(--accent-cyan); flex-shrink: 0; }
.user-cfg button.danger svg { color: var(--accent-red); }
.user-cfg button.danger:hover { color: var(--accent-red); }
.menu-sep { height: 1px; background: var(--line); margin: 4px 2px; flex-shrink: 0; }

.start-item {
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
.start-item:hover { background: var(--surface-2); color: var(--text-0); }
.start-item-icon { width: 17px; height: 17px; color: var(--accent-cyan); flex-shrink: 0; }
.exit-item { color: var(--text-1); }
.exit-item .exit-icon { width: 17px; height: 17px; color: var(--accent-red); flex-shrink: 0; }
.exit-item:hover { background: rgba(236, 1, 1, 0.12); color: var(--accent-red); }

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
  max-width: 160px;
}
.taskbar-tab:hover { color: var(--text-0); border-color: var(--line-strong); }
.taskbar-tab.minimized { opacity: 0.55; }
.taskbar-tab-icon { width: 14px; height: 14px; color: var(--accent-cyan); flex-shrink: 0; }

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
  box-shadow: 0 0 8px rgba(71, 212, 185, 0.5);
}
.tray-item.steam.ok .tray-value { color: var(--accent-green); font-weight: 600; }
.tray-label { display: none; }
@media (min-width: 1100px) { .tray-label { display: inline; } }

.tray-item.conn .dot {
  width: 6px; height: 6px; border-radius: 50%;
  background: var(--accent-red);
}
.tray-item.conn.ok .dot { background: var(--accent-green); box-shadow: 0 0 8px rgba(71, 212, 185, 0.5); }

.tray-item.clock {
  font-family: var(--font-mono);
  color: var(--text-1);
  min-width: 64px;
  text-align: right;
}
</style>
