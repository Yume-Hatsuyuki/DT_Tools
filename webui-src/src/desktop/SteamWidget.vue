<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useSteamPlayers } from '../composables/useSteamPlayers.js';
import IconX from '~icons/tabler/x';

/**
 * Steam 在线人数小组件（插件式弹窗）：从顶栏托盘拆出——可拖动、可关闭，
 * 关闭后经底部 Dock 重新打开。红/绿霓虹光效替代原来的绿点：数据正常呼吸
 * 绿光，接口不可用转红光，一眼可辨。位置记忆在 localStorage（仅本浏览器），
 * 未拖动过时默认悬停右上角、与菜单栏留出间距。
 */
const emit = defineEmits(['close']);

const { count, ok } = useSteamPlayers();

const STORE_KEY = 'dt_steam_widget_v1';
// 与 Window.vue 的 TOPBAR_H / tokens.css --topbar-h 同步：弹窗不拖进顶栏之下
const TOPBAR_H = 32;

function load() {
  try { return JSON.parse(localStorage.getItem(STORE_KEY)) || {}; } catch { return {}; }
}

const pos = ref(load().pos || null);   // {left, top}；null=默认右上角
const rootEl = ref(null);

function persist(closed) {
  try {
    localStorage.setItem(STORE_KEY, JSON.stringify({ closed, pos: pos.value }));
  } catch { /* 隐私模式忽略 */ }
}

onMounted(() => persist(false));

function close() {
  persist(true);
  emit('close');
}

const posStyle = computed(() =>
  pos.value ? { left: pos.value.left + 'px', top: pos.value.top + 'px', right: 'auto' } : {});

// ---- 标题栏拖动（视口内钳位） ----

let drag = null;

function onHeadDown(e) {
  if (e.target.closest('button')) return;   // 关闭按钮不触发拖动
  const r = rootEl.value.getBoundingClientRect();
  drag = { dx: e.clientX - r.left, dy: e.clientY - r.top };
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}
function onMove(e) {
  const el = rootEl.value;
  const left = Math.min(Math.max(8, e.clientX - drag.dx), window.innerWidth - el.offsetWidth - 8);
  const top = Math.min(Math.max(TOPBAR_H + 8, e.clientY - drag.dy), window.innerHeight - el.offsetHeight - 8);
  pos.value = { left, top };
}
function onUp() {
  drag = null;
  window.removeEventListener('pointermove', onMove);
  window.removeEventListener('pointerup', onUp);
  persist(false);
}
onUnmounted(() => {
  window.removeEventListener('pointermove', onMove);
  window.removeEventListener('pointerup', onUp);
});

const countText = computed(() =>
  ok.value && count.value != null ? count.value.toLocaleString('en-US') : '--');
</script>

<template>
  <div ref="rootEl" class="steam-widget" :class="ok ? 'ok' : 'err'" :style="posStyle">
    <div class="sw-head" @pointerdown="onHeadDown">
      <span class="sw-title">《Deadly Trick》在线</span>
      <button class="sw-close" title="关闭（可从底部 Dock 再次打开）" @click="close"><IconX /></button>
    </div>
    <div class="sw-body">
      <div class="sw-count">{{ countText }}</div>
      <div class="sw-sub">Steam 全球在线玩家 · 每分钟刷新</div>
    </div>
  </div>
</template>

<style scoped>
.steam-widget {
  position: fixed;
  z-index: 4500;   /* 悬于窗口与粒子之上，低于顶栏/Dock */
  width: 208px;
  /* 默认悬停右上角，与菜单栏留出间距；拖动后由内联 left/top 接管（right:auto） */
  top: calc(var(--topbar-h) + 14px);
  right: 14px;
  border-radius: 14px;
  background: rgba(9, 18, 26, 0.78);
  backdrop-filter: blur(20px) saturate(150%);
  border: 1px solid var(--dock-border);
  box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7);
  user-select: none;
}

/* 红/绿霓虹（虚幻）光效：取代原托盘的绿点，状态一眼可辨 */
.steam-widget.ok {
  border-color: rgba(71, 212, 185, 0.55);
  animation: neon-green 2.8s ease-in-out infinite;
}
.steam-widget.err {
  border-color: rgba(255, 82, 82, 0.55);
  animation: neon-red 2.8s ease-in-out infinite;
}
@keyframes neon-green {
  0%, 100% { box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7), 0 0 10px rgba(71, 212, 185, 0.25), inset 0 0 12px rgba(71, 212, 185, 0.10); }
  50% { box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7), 0 0 26px rgba(71, 212, 185, 0.55), inset 0 0 20px rgba(71, 212, 185, 0.20); }
}
@keyframes neon-red {
  0%, 100% { box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7), 0 0 10px rgba(255, 82, 82, 0.25), inset 0 0 12px rgba(255, 82, 82, 0.10); }
  50% { box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7), 0 0 26px rgba(255, 82, 82, 0.55), inset 0 0 20px rgba(255, 82, 82, 0.20); }
}

.sw-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 8px 6px 12px;
  cursor: grab;
}
.steam-widget:active .sw-head { cursor: grabbing; }
.sw-title {
  flex: 1;
  font-size: 11px;
  color: var(--text-2);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sw-close {
  width: 20px;
  height: 20px;
  display: grid;
  place-items: center;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
}
.sw-close:hover { background: rgba(255, 82, 82, 0.15); color: var(--accent-red); }
.sw-close svg { width: 12px; height: 12px; }

.sw-body { padding: 2px 14px 12px; }
.sw-count {
  font-family: var(--font-mono);
  font-size: 27px;
  font-weight: 700;
  line-height: 1.2;
  color: var(--accent-green);
  text-shadow: 0 0 14px rgba(71, 212, 185, 0.55);
}
.steam-widget.err .sw-count { color: var(--accent-red); text-shadow: 0 0 14px rgba(255, 82, 82, 0.5); }
.sw-sub { font-size: 10.5px; color: var(--text-2); margin-top: 2px; }
</style>
