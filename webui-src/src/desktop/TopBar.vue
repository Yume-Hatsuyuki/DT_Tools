<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useConnectionStatus } from '../composables/useConnectionStatus.js';
import { useShellUser } from '../composables/useShellUser.js';
import { useSectionMeta } from '../composables/useSectionMeta.js';
import { useWindowManager } from '../composables/useWindowManager.js';
import { pickImageFile } from '../composables/pickImage.js';
import { useCropper } from '../composables/useCropper.js';
import { useUpdateCheck } from '../composables/useUpdateCheck.js';
import { API } from '../api.js';
import IconDragon from '~icons/tabler/dragon';
import IconWifi from '~icons/tabler/wifi';
import IconUser from '~icons/tabler/user';
import IconUserEdit from '~icons/tabler/user-edit';
import IconPhoto from '~icons/tabler/photo';
import IconPhotoOff from '~icons/tabler/photo-off';
import IconPower from '~icons/tabler/power';
import IconInfoCircle from '~icons/tabler/info-circle';
import IconSearch from '~icons/tabler/search';
import IconLayoutGrid from '~icons/tabler/layout-grid';
import IconPuzzle from '~icons/tabler/puzzle';
import IconSettings from '~icons/tabler/settings';
import IconArrowBarToUp from '~icons/tabler/arrow-bar-to-up';
import IconArrowBarToDown from '~icons/tabler/arrow-bar-to-down';
import IconMaximize from '~icons/tabler/maximize';
import IconMinimize from '~icons/tabler/minimize';
import IconX from '~icons/tabler/x';
import IconDownload from '~icons/tabler/download';

/**
 * 顶栏（macOS 菜单栏 / Kali 面板位）。
 * 左：DT 龙标（Kali 应用菜单风大面板：搜索 + 分类「应用/小组件/系统」+ 功能列表）
 *     + 静态品牌名 DT_Tools（图3 效果，不挂任何菜单）；
 *     面板底部用户栏 = 可点用户行（弹出账户菜单：改名/头像/退出，图2 效果）
 *     + 独立退出游戏按钮（红，右下角，Kali 电源键位）。
 * 中：运行中窗口标签（点击 聚焦/最小化 切换；右键 独立窗口操作菜单：
 *     切换前置/最大化/最小化/关闭窗口——与桌面、Dock 的右键菜单互不相通）。
 * 右：连接状态（图标式：绿=已连接 / 红=连接中）+ 中文时间（x月x日周x HH:mm）。
 */
const props = defineProps({
  /** 桌面应用注册表 [{id,title,icon}]（Kali 面板的"应用"类目）。 */
  apps: { type: Array, required: true },
  windows: { type: Array, required: true },
  /** Steam 在线小组件是否开着（面板里的开关态展示）。 */
  steamWidgetOpen: { type: Boolean, default: false },
  mcpWidgetOpen: { type: Boolean, default: false },
});
const emit = defineEmits(['focus-window', 'about', 'show-update', 'change-wallpaper', 'reset-wallpaper', 'toggle-widget', 'launch']);

const conn = useConnectionStatus();
const shell = useShellUser();
const cropper = useCropper();
const upd = useUpdateCheck();   // 更新徽标/气泡只读全局状态（轮询由 Desktop 启动）
const metaStore = useSectionMeta();
const wm = useWindowManager();   // 窗口操作（模块级单例，与 Desktop 持有同一份状态）

// 中文时间（学习 NoriOS 顶栏：9月30日周三 13:28），1s 刷新
const now = ref(new Date());
let timer = null;
onMounted(() => { timer = setInterval(() => { now.value = new Date(); }, 1000); });
onUnmounted(() => clearInterval(timer));

const WEEK = '日一二三四五六';
const clock = computed(() => {
  const d = now.value;
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getMonth() + 1}月${d.getDate()}日周${WEEK[d.getDay()]} ${p(d.getHours())}:${p(d.getMinutes())}`;
});

// ---- 菜单开合（Kali 面板 / 面板底部账户菜单；外点关闭）----
const logoOpen = ref(false);
const acctOpen = ref(false);

function toggleLogo() {
  logoOpen.value = !logoOpen.value;
  if (logoOpen.value) acctOpen.value = false;
}
function toggleAcct() {
  acctOpen.value = !acctOpen.value;
}
function closeMenus() {
  logoOpen.value = false;
  acctOpen.value = false;
}

// ---- 更新徽标 / 气泡（有新版本时出现在品牌名旁；点击打开更新弹窗并收起气泡）----

const latestTag = computed(() => (upd.state.latest && upd.state.latest.tag) || '');

function openUpdate() {
  closeMenus();
  upd.dismissBubble();
  emit('show-update');
}

// 外点关闭：菜单挂在顶栏组件里，点桌面/窗口不经过顶栏的 @click，
// 需文档级捕获监听（点在顶栏与面板内不放行——菜单项的 click 先于卸载发生）
function onDocPointerDown(e) {
  const t = e.target;
  if (t.closest && (t.closest('.topbar') || t.closest('.topbar-menu'))) return;
  closeMenus();
}
watch([logoOpen, acctOpen], ([l, a]) => {
  if (l || a) document.addEventListener('pointerdown', onDocPointerDown, true);
  else document.removeEventListener('pointerdown', onDocPointerDown, true);
});
onUnmounted(() => document.removeEventListener('pointerdown', onDocPointerDown, true));

// ---- 运行标签右键菜单：窗口操作（独立菜单，不复用桌面 .ctx-menu）----

const winMenu = ref(null);   // { x, y, id }

function onTabContextMenu(e, w) {
  // stopPropagation：不让事件冒泡到 .desktop，否则桌面默认菜单会跟着弹出
  e.preventDefault();
  e.stopPropagation();
  winMenu.value = {
    x: Math.min(e.clientX, window.innerWidth - 190),
    y: Math.min(e.clientY, window.innerHeight - 200),
    id: w.id,
  };
}
function closeWinMenu() { winMenu.value = null; }

const menuWindow = computed(() =>
  winMenu.value ? props.windows.find(w => w.id === winMenu.value.id) || null : null);

const winMenuItems = computed(() => {
  const w = menuWindow.value;
  if (!w) return [];
  return [
    { label: '切换前置', icon: IconArrowBarToUp, action: () => { closeWinMenu(); wm.focus(w.id); } },
    { label: w.maximized ? '还原' : '最大化', icon: w.maximized ? IconMinimize : IconMaximize, action: () => { closeWinMenu(); wm.toggleMaximize(w.id); } },
    { label: '最小化', icon: IconArrowBarToDown, action: () => { closeWinMenu(); wm.minimize(w.id); } },
    { label: '关闭窗口', icon: IconX, danger: true, action: () => { closeWinMenu(); wm.close(w.id); } },
  ];
});

// ---- Kali 风面板：分类 + 搜索 + 功能列表 ----

const CATS = [
  { id: 'app', label: '应用', icon: IconLayoutGrid },
  { id: 'widget', label: '小组件', icon: IconPuzzle },
  { id: 'system', label: '系统', icon: IconSettings },
];
const activeCat = ref('app');
const kmQuery = ref('');

function appMetaKey(appId) { return 'app:' + appId; }
function appLabel(appId, fallback) { return metaStore.displayName(appMetaKey(appId)) || fallback; }
function appIconSrc(appId) { return (metaStore.get(appMetaKey(appId)) || {}).icon || null; }

function runApp(appId) {
  closeMenus();
  emit('launch', appId);
}

const SYSTEM_ITEMS = [
  { id: 'wallpaper', label: '更换壁纸…', icon: IconPhoto },
  { id: 'wallpaper-reset', label: '恢复默认壁纸', icon: IconPhotoOff },
  { id: 'about', label: '关于 DT_Tools…', icon: IconInfoCircle },
  { id: 'exit', label: '退出游戏', icon: IconPower },
];

const kmItems = computed(() => {
  const q = kmQuery.value.trim().toLowerCase();
  const rows = [];
  const pushApps = (filter) => {
    for (const a of props.apps) {
      const label = appLabel(a.id, a.title);
      if (filter && !label.toLowerCase().includes(q) && !a.id.includes(q)) continue;
      rows.push({ kind: 'app', id: a.id, label, img: appIconSrc(a.id), icon: a.icon });
    }
  };
  if (q) {
    // 搜索：跨类过滤（结果行右侧带分类标注）
    pushApps(true);
    if ('steam 在线'.toLowerCase().includes(q) || 'steam'.includes(q) || '小组件'.includes(q))
      rows.push({ kind: 'widget', id: 'steam', label: 'Steam 在线', icon: IconPuzzle, tag: '小组件' });
    if ('mcp 桥接 ai 接入'.toLowerCase().includes(q) || 'mcp'.includes(q) || '小组件'.includes(q))
      rows.push({ kind: 'widget', id: 'mcp', label: 'MCP 桥接（AI 接入）', icon: IconPuzzle, tag: '小组件' });
    for (const s of SYSTEM_ITEMS)
      if (s.label.toLowerCase().includes(q)) rows.push({ kind: 'system', id: s.id, label: s.label, icon: s.icon, tag: '系统' });
    return rows;
  }
  if (activeCat.value === 'app') pushApps(false);
  if (activeCat.value === 'widget') {
    rows.push({ kind: 'widget', id: 'steam', label: 'Steam 在线', icon: IconPuzzle });
    rows.push({ kind: 'widget', id: 'mcp', label: 'MCP 桥接（AI 接入）', icon: IconPuzzle });
  }
  if (activeCat.value === 'system')
    for (const s of SYSTEM_ITEMS) rows.push({ kind: 'system', id: s.id, label: s.label, icon: s.icon });
  return rows;
});

function widgetStateText(id) {
  if (id === 'steam') return props.steamWidgetOpen ? '已开启' : '已关闭';
  if (id === 'mcp') return props.mcpWidgetOpen ? '已开启' : '已关闭';
  return '';
}

function runItem(item) {
  if (item.kind === 'app') { runApp(item.id); return; }
  if (item.kind === 'widget') { emit('toggle-widget', item.id); return; }
  closeMenus();
  if (item.id === 'wallpaper') emit('change-wallpaper');
  else if (item.id === 'wallpaper-reset') emit('reset-wallpaper');
  else if (item.id === 'about') emit('about');
  else if (item.id === 'exit') exitGame();
}

// ---- 账户操作（面板底部用户栏）----

async function renameUser() {
  closeMenus();
  const name = prompt('修改用户名（终端提示符同步更新）：', shell.state.user);
  if (name === null) return;
  const r = shell.setUser(name);
  if (!r.ok) alert(r.error);
}

async function changeAvatar() {
  closeMenus();
  const file = await pickImageFile();
  if (!file) return;
  const dataUrl = await cropper.open(file, { title: '裁切头像', shape: 'circle' });
  if (dataUrl) shell.setAvatar(dataUrl);
}
function resetAvatar() {
  closeMenus();
  shell.setAvatar(null);
}

async function exitGame() {
  closeMenus();
  if (!confirm('确定退出游戏？未保存的配置将丢失（可在退出前先点"保存"）。')) return;
  const j = await API.gameExit();
  if (!j || !j.ok) {
    alert('退出指令发送失败：' + ((j && j.error) || '未知错误'));
  }
}
</script>

<template>
  <div class="topbar" @click="closeMenus">
    <!-- 左：龙标 + 静态品牌名（图3，无菜单） -->
    <div class="topbar-left">
      <button class="brand" :class="{ open: logoOpen }" title="应用菜单" @click.stop="toggleLogo">
        <IconDragon />
      </button>
      <span class="brand-name">DT_Tools</span>
      <!-- 更新徽标：存在新版本时呼吸绿光，点击打开更新弹窗 -->
      <button
        v-if="upd.state.hasUpdate"
        class="upd-badge"
        :title="'发现新版本 ' + latestTag + '，点击查看'"
        @click.stop="openUpdate"
      >
        <IconDownload />
      </button>

      <!-- 更新气泡：徽标出现后从品牌名下方弹出，说明有新版本；点开弹窗或 × 关闭 -->
      <div v-if="upd.state.hasUpdate && !upd.state.bubbleDismissed" class="upd-bubble" @click.stop="openUpdate">
        <span class="upd-bubble-text">发现新版本 <b>{{ latestTag }}</b>，点击查看更新内容</span>
        <button class="upd-bubble-x" title="知道了" @click.stop="upd.dismissBubble()"><IconX /></button>
      </div>
    </div>

    <!-- Kali 应用菜单风面板：搜索 + 分类导航 + 功能列表 + 底部用户栏 -->
    <div v-if="logoOpen" class="topbar-menu kali-panel" @click.stop="acctOpen = false">
      <div class="km-search">
        <IconSearch class="km-search-icon" />
        <input v-model="kmQuery" type="search" placeholder="搜索应用 / 功能…" @keydown.esc="closeMenus">
      </div>
      <div class="km-body">
        <div class="km-side">
          <button
            v-for="c in CATS"
            :key="c.id"
            class="km-side-item"
            :class="{ active: !kmQuery.trim() && activeCat === c.id }"
            @click="activeCat = c.id; kmQuery = ''"
          >
            <component :is="c.icon" class="km-side-icon" />
            <span>{{ c.label }}</span>
          </button>
          <div class="km-side-grow" />
          <!-- 底部用户栏：用户行点开账户菜单（图2），右侧独立退出按钮（Kali 电源键位） -->
          <div class="km-user">
            <button class="km-user-btn" :class="{ open: acctOpen }" title="账户" @click.stop="toggleAcct">
              <img v-if="shell.state.avatar" :src="shell.state.avatar" alt="" class="km-user-avatar">
              <IconUser v-else class="km-user-fallback" />
              <span class="km-user-name">{{ shell.state.user }}@{{ shell.state.hostname }}</span>
            </button>
            <button class="km-exit" title="退出游戏" @click.stop="exitGame"><IconPower /></button>

            <div v-if="acctOpen" class="km-acct-menu" @click.stop>
              <button @click="renameUser"><IconUserEdit /> 修改用户名…</button>
              <button @click="changeAvatar"><IconPhoto /> 更换头像…</button>
              <button v-if="shell.state.avatar" @click="resetAvatar"><IconPhotoOff /> 恢复默认头像</button>
            </div>
          </div>
        </div>
        <div class="km-list">
          <button v-for="item in kmItems" :key="item.kind + ':' + item.id" class="km-item" @click="runItem(item)">
            <span class="km-item-icon">
              <img v-if="item.img" :src="item.img" alt="" class="km-item-img">
              <component :is="item.icon" v-else class="km-item-fallback" />
            </span>
            <span class="km-item-label">{{ item.label }}</span>
            <span v-if="item.kind === 'widget'" class="km-item-state" :class="{ on: item.id === 'steam' ? steamWidgetOpen : mcpWidgetOpen }">{{ widgetStateText(item.id) }}</span>
            <span v-else-if="item.tag" class="km-item-state">{{ item.tag }}</span>
          </button>
          <div v-if="!kmItems.length" class="km-empty">无匹配项。</div>
        </div>
      </div>
    </div>

    <!-- 中：运行中窗口标签 -->
    <div class="topbar-running">
      <button
        v-for="w in windows"
        :key="w.id"
        class="topbar-tab"
        :class="{ minimized: w.minimized }"
        :title="w.title"
        @click.stop="emit('focus-window', w.id)"
        @contextmenu="onTabContextMenu($event, w)"
      >
        <component :is="w.icon" v-if="w.icon" class="topbar-tab-icon" />
        {{ w.title }}
      </button>
    </div>

    <!-- 运行标签右键菜单：Teleport 到 body（顶栏 backdrop-filter 会劫持 fixed 包含块，
         Dock 右键菜单同款先例）；遮罩负责外点/右键关闭 -->
    <Teleport to="body">
      <div v-if="winMenu" class="win-menu-mask" @click="closeWinMenu" @contextmenu.prevent="closeWinMenu" />
      <div
        v-if="winMenu"
        class="win-menu"
        :style="{ left: winMenu.x + 'px', top: winMenu.y + 'px' }"
        @click.stop
        @contextmenu.prevent
      >
        <button
          v-for="mi in winMenuItems"
          :key="mi.label"
          type="button"
          :class="{ danger: mi.danger }"
          @click="mi.action()"
        >
          <component :is="mi.icon" class="win-menu-icon" />
          {{ mi.label }}
        </button>
      </div>
    </Teleport>

    <!-- 右：连接状态（图标式）+ 中文时间 -->
    <div class="topbar-tray">
      <div class="tray-item conn" :class="{ ok: conn.online }" :title="conn.online ? '已连接' : '连接中…'">
        <IconWifi />
      </div>
      <div class="tray-item clock">{{ clock }}</div>
    </div>
  </div>
</template>

<style scoped>
.topbar {
  position: fixed;
  left: 0; right: 0; top: 0;
  height: var(--topbar-h);
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 10px;
  background: var(--topbar-bg);
  border-bottom: 1px solid var(--topbar-border);
  box-shadow: 0 8px 24px rgba(2, 10, 18, 0.45);
  backdrop-filter: blur(20px) saturate(150%);
  z-index: 5000;
  font-size: 12px;
}

.topbar-left { display: flex; align-items: center; gap: 7px; flex-shrink: 0; position: relative; }

.brand {
  display: grid;
  place-items: center;
  width: 24px;
  height: 24px;
  /* 必须清掉浏览器默认 button padding（1px 6px）——它会把 17px 的图标挤离中心 */
  padding: 0;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  color: var(--shell-glow);
  filter: drop-shadow(0 0 6px var(--shell-glow-dim));
  cursor: pointer;
}
.brand svg { width: 17px; height: 17px; }
.brand:hover, .brand.open { background: rgba(150, 225, 255, 0.10); border-color: rgba(150, 225, 255, 0.22); }

.brand-name {
  font-family: var(--font-mono);
  font-size: 12.5px;
  font-weight: 600;
  letter-spacing: 0.02em;
  color: var(--text-0);
  user-select: none;
}

/* ---- 更新徽标 + 气泡（有新版本时出现）---- */

.upd-badge {
  display: grid;
  place-items: center;
  width: 18px;
  height: 18px;
  padding: 0;
  background: rgba(71, 212, 185, 0.14);
  border: 1px solid rgba(71, 212, 185, 0.45);
  border-radius: 50%;
  color: var(--accent-green);
  cursor: pointer;
  animation: upd-pulse 2s ease-in-out infinite;
}
.upd-badge svg { width: 12px; height: 12px; }
.upd-badge:hover { background: rgba(71, 212, 185, 0.28); border-color: rgba(71, 212, 185, 0.7); }
@keyframes upd-pulse {
  0%, 100% { box-shadow: 0 0 0 0 rgba(71, 212, 185, 0.0); }
  50% { box-shadow: 0 0 8px 2px rgba(71, 212, 185, 0.35); }
}

/* 气泡锚在品牌行正下方（topbar-left 已设 relative）；箭头大致对准徽标 */
.upd-bubble {
  position: absolute;
  top: calc(100% + 9px);
  left: 0;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 10px;
  background: var(--win-bg);
  border: 1px solid rgba(71, 212, 185, 0.55);
  border-radius: var(--radius-md);
  box-shadow: var(--menu-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  color: var(--text-1);
  font-size: 12px;
  white-space: nowrap;
  cursor: pointer;
  z-index: 5500;
  animation: upd-bubble-in 0.25s ease-out;
}
.upd-bubble::before {
  content: '';
  position: absolute;
  top: -4.5px;
  left: 100px;
  width: 8px;
  height: 8px;
  background: var(--win-bg);
  border-left: 1px solid rgba(71, 212, 185, 0.55);
  border-top: 1px solid rgba(71, 212, 185, 0.55);
  transform: rotate(45deg);
}
.upd-bubble:hover { border-color: rgba(71, 212, 185, 0.85); color: var(--text-0); }
.upd-bubble-text b { color: var(--accent-green); font-family: var(--font-mono); font-weight: 600; }
.upd-bubble-x {
  display: grid;
  place-items: center;
  width: 18px;
  height: 18px;
  padding: 0;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
  flex-shrink: 0;
}
.upd-bubble-x svg { width: 12px; height: 12px; }
.upd-bubble-x:hover { background: rgba(150, 225, 255, 0.10); color: var(--text-0); }
@keyframes upd-bubble-in {
  from { opacity: 0; transform: translateY(-5px); }
  to { opacity: 1; transform: none; }
}

.topbar-menu {
  position: fixed;
  top: calc(var(--topbar-h) + 6px);
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--menu-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  padding: 5px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  z-index: 6000;
}
.topbar-menu button {
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
.topbar-menu button:hover { background: rgba(150, 225, 255, 0.10); color: var(--text-0); }
.topbar-menu button svg { width: 15px; height: 15px; color: var(--shell-glow); flex-shrink: 0; }

/* ---- Kali 应用菜单风面板 ---- */

.kali-panel {
  left: 8px;
  width: 540px;
  height: 440px;
  max-height: calc(100vh - var(--topbar-h) - 20px);
  padding: 10px;
  gap: 8px;
}

.km-search {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  padding: 7px 10px;
  flex-shrink: 0;
}
.km-search:focus-within { border-color: var(--accent-cyan-dim); }
.km-search-icon { width: 14px; height: 14px; color: var(--text-2); flex-shrink: 0; }
.km-search input {
  flex: 1;
  background: transparent;
  border: none;
  outline: none;
  color: var(--text-0);
  font-size: 12.5px;
  font-family: var(--font-ui);
}
.km-search input::placeholder { color: var(--text-2); }

.km-body { display: flex; flex: 1; min-height: 0; gap: 8px; }

.km-side {
  width: 196px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
  border-right: 1px solid var(--line);
  padding-right: 8px;
}
.km-side-item {
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 8px 10px;
  background: transparent;
  border: none;
  border-left: 2px solid transparent;
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  cursor: pointer;
}
.km-side-item:hover { background: rgba(150, 225, 255, 0.08); color: var(--text-0); }
.km-side-item.active {
  background: rgba(150, 225, 255, 0.10);
  border-left-color: var(--shell-glow);
  color: var(--text-0);
}
.km-side-icon { width: 15px; height: 15px; color: var(--shell-glow); flex-shrink: 0; }
.km-side-grow { flex: 1; }

/* 底部用户栏：可点用户行 + 独立退出按钮 */
.km-user {
  position: relative;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 0 2px;
  border-top: 1px solid var(--line);
}
.km-user-btn {
  display: flex;
  align-items: center;
  gap: 7px;
  flex: 1;
  min-width: 0;
  height: 30px;
  padding: 0 6px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.km-user-btn:hover, .km-user-btn.open { background: rgba(150, 225, 255, 0.10); border-color: rgba(150, 225, 255, 0.22); }
.km-user-avatar { width: 20px; height: 20px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }
.km-user-fallback { width: 17px; height: 17px; color: var(--shell-glow); flex-shrink: 0; }
.km-user-name {
  font-family: var(--font-mono);
  font-size: 11.5px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
/* 独立退出键：常红（破坏性操作一眼可辨），与用户行同高对齐。
   选择器带 .km-user 提高特异性——否则会被 .topbar-menu button 的 text-1 覆盖 */
.km-user .km-exit {
  width: 30px;
  height: 30px;
  padding: 0;
  display: grid;
  place-items: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  color: var(--accent-red);
  cursor: pointer;
  flex-shrink: 0;
}
.km-user .km-exit svg { width: 15px; height: 15px; color: var(--accent-red); }
.km-user .km-exit:hover { background: rgba(236, 1, 1, 0.14); border-color: rgba(236, 1, 1, 0.35); filter: drop-shadow(0 0 5px rgba(255, 82, 82, 0.45)); }

/* 账户菜单：锚在用户行上方弹出（行在面板底部，向上展开才不越界） */
.km-acct-menu {
  position: absolute;
  left: 0;
  bottom: calc(100% + 8px);
  width: 210px;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--menu-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  padding: 5px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  z-index: 20;
}

.km-list { flex: 1; min-width: 0; overflow-y: auto; display: flex; flex-direction: column; gap: 2px; }
.km-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 7px 10px;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  cursor: pointer;
}
.km-item:hover { background: rgba(150, 225, 255, 0.10); color: var(--text-0); }
.km-item-icon {
  width: 28px;
  height: 28px;
  display: grid;
  place-items: center;
  border-radius: 8px;
  background: radial-gradient(circle at 50% 40%,
    rgba(120, 210, 240, 0.28) 0%,
    rgba(52, 120, 160, 0.40) 48%,
    rgba(14, 38, 56, 0.85) 100%);
  border: 1px solid rgba(190, 235, 255, 0.20);
  flex-shrink: 0;
}
.km-item-fallback { width: 15px; height: 15px; color: #dff6ff; }
.km-item-img { width: 100%; height: 100%; object-fit: cover; border-radius: 8px; }
.km-item-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.km-item-state { font-size: 10.5px; color: var(--text-2); flex-shrink: 0; }
.km-item-state.on { color: var(--accent-green); }
.km-empty { text-align: center; color: var(--text-2); padding: 30px 0; font-size: 12px; }

.topbar-running {
  flex: 1;
  display: flex;
  justify-content: center;
  gap: 6px;
  overflow-x: auto;
  min-width: 0;
}
.topbar-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 22px;
  padding: 0 10px;
  background: rgba(150, 225, 255, 0.08);
  border: 1px solid rgba(150, 225, 255, 0.14);
  border-radius: 999px;
  color: var(--text-1);
  font-size: 11.5px;
  white-space: nowrap;
  cursor: pointer;
  flex-shrink: 0;
  max-width: 160px;
  overflow: hidden;
}
.topbar-tab:hover { color: var(--text-0); border-color: rgba(150, 225, 255, 0.30); }
.topbar-tab.minimized { opacity: 0.5; }
.topbar-tab-icon { width: 12px; height: 12px; color: var(--shell-glow); flex-shrink: 0; }

/* 运行标签右键菜单（Teleport 到 body；造型与桌面/Dock 菜单同源，实例相互独立） */
.win-menu-mask { position: fixed; inset: 0; z-index: 9998; }
.win-menu {
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
.win-menu button {
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
.win-menu button:hover { background: rgba(150, 225, 255, 0.10); color: var(--text-0); }
.win-menu button .win-menu-icon { width: 14px; height: 14px; color: var(--shell-glow); flex-shrink: 0; }
/* 关闭窗口：破坏性操作，红色可辨 */
.win-menu button.danger { color: var(--accent-red); }
.win-menu button.danger .win-menu-icon { color: var(--accent-red); }
.win-menu button.danger:hover { background: rgba(236, 1, 1, 0.12); color: var(--accent-red); }

.topbar-tray {
  display: flex;
  align-items: center;
  gap: 14px;
  flex-shrink: 0;
  color: var(--text-2);
}
.tray-item { display: flex; align-items: center; gap: 6px; }

/* 连接状态：图标式（学习 NoriOS 顶栏的信号图标），绿=已连接 / 红=连接中 */
.tray-item.conn svg { width: 15px; height: 15px; color: var(--accent-red); }
.tray-item.conn.ok svg {
  color: var(--accent-green);
  filter: drop-shadow(0 0 5px rgba(71, 212, 185, 0.6));
}

.tray-item.clock {
  font-family: var(--font-mono);
  color: var(--text-1);
  white-space: nowrap;
}
</style>
