<script setup>
import { computed, provide } from 'vue';
import IconMinus from '~icons/tabler/minus';
import IconSquare from '~icons/tabler/square';
import IconSquaresDiagonal from '~icons/tabler/squares-diagonal';
import IconX from '~icons/tabler/x';

const props = defineProps({
  win: { type: Object, required: true },
});
const emit = defineEmits(['close', 'focus', 'minimize', 'toggle-maximize', 'update-geometry']);

// 给窗口内应用（如终端的 exit 内置命令）一个受控的自我操作通道
provide('winApi', {
  close: () => emit('close'),
  minimize: () => emit('minimize'),
  toggleMaximize: () => emit('toggle-maximize'),
});

const MIN_W = 360;
const MIN_H = 240;

const style = computed(() => {
  if (props.win.maximized) {
    return { left: '0', top: '0', width: '100%', height: '100%', zIndex: props.win.z };
  }
  return {
    left: props.win.x + 'px',
    top: props.win.y + 'px',
    width: props.win.w + 'px',
    height: props.win.h + 'px',
    zIndex: props.win.z,
  };
});

function onTitlebarPointerDown(e) {
  emit('focus');
  if (props.win.maximized) return;
  if (e.target.closest('.win-controls')) return;
  const startX = e.clientX, startY = e.clientY;
  const startWinX = props.win.x, startWinY = props.win.y;
  const onMove = (ev) => {
    emit('update-geometry', {
      x: Math.max(0, startWinX + (ev.clientX - startX)),
      y: Math.max(0, startWinY + (ev.clientY - startY)),
    });
  };
  const onUp = () => {
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
  };
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}

/**
 * 八方向缩放：n/s/e/w 四边 + 四角。统一走一份几何计算——
 * 各方向决定 x/y/w/h 中哪些量跟随鼠标，其余保持；越界按最小尺寸回推。
 */
const DIRS = ['n', 's', 'e', 'w', 'ne', 'nw', 'se', 'sw'];

function onResizePointerDown(dir, e) {
  e.stopPropagation();
  emit('focus');
  if (props.win.maximized) return;
  const startX = e.clientX, startY = e.clientY;
  const s = { x: props.win.x, y: props.win.y, w: props.win.w, h: props.win.h };
  const goEast = dir.includes('e');
  const goWest = dir.includes('w');
  const goSouth = dir.includes('s');
  const goNorth = dir.includes('n');

  const onMove = (ev) => {
    const dx = ev.clientX - startX;
    const dy = ev.clientY - startY;
    const patch = {};
    if (goEast) patch.w = Math.max(MIN_W, s.w + dx);
    if (goSouth) patch.h = Math.max(MIN_H, s.h + dy);
    if (goWest) {
      // 宽度到最小值后继续向右拖：窗口跟着右移，保持光标下的边不"脱手"
      const w = Math.max(MIN_W, s.w - dx);
      patch.w = w;
      patch.x = s.x + (s.w - w);
    }
    if (goNorth) {
      const h = Math.max(MIN_H, s.h - dy);
      patch.h = h;
      patch.y = Math.max(0, s.y + (s.h - h));
    }
    emit('update-geometry', patch);
  };
  const onUp = () => {
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
  };
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}
</script>

<template>
  <div
    v-show="!win.minimized"
    class="win"
    :class="{ maximized: win.maximized }"
    :style="style"
    @pointerdown="emit('focus')"
  >
    <div class="win-titlebar" @pointerdown="onTitlebarPointerDown" @dblclick="emit('toggle-maximize')">
      <component :is="win.icon" v-if="win.icon" class="win-icon" />
      <span class="win-title">{{ win.title }}</span>
      <div class="win-controls">
        <button class="win-btn" title="最小化" @click="emit('minimize')"><IconMinus /></button>
        <button class="win-btn" :title="win.maximized ? '还原' : '最大化'" @click="emit('toggle-maximize')">
          <IconSquaresDiagonal v-if="win.maximized" />
          <IconSquare v-else />
        </button>
        <button class="win-btn win-btn-close" title="关闭" @click="emit('close')"><IconX /></button>
      </div>
    </div>
    <div class="win-body">
      <slot />
    </div>
    <template v-if="!win.maximized">
      <div
        v-for="dir in DIRS"
        :key="dir"
        class="win-rz"
        :class="'rz-' + dir"
        @pointerdown="onResizePointerDown(dir, $event)"
      />
    </template>
  </div>
</template>

<style scoped>
.win {
  position: absolute;
  display: flex;
  flex-direction: column;
  background: var(--win-bg);
  border: 1px solid var(--win-border);
  border-radius: var(--win-radius);
  box-shadow: var(--win-shadow);
  backdrop-filter: blur(18px) saturate(140%);
  overflow: hidden;
  min-width: 360px;
  min-height: 240px;
}
.win.maximized { border-radius: 0; border: none; }

.win-titlebar {
  display: flex;
  align-items: center;
  gap: 8px;
  height: 40px;
  padding: 0 8px 0 14px;
  background: var(--win-titlebar);
  border-bottom: 1px solid var(--line);
  cursor: grab;
  user-select: none;
  flex-shrink: 0;
}
.win:active .win-titlebar { cursor: grabbing; }
.win-icon { width: 16px; height: 16px; color: var(--accent-cyan); flex-shrink: 0; }
.win-title {
  font-size: 12.5px;
  font-weight: 600;
  letter-spacing: 0.02em;
  color: var(--text-1);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.win-controls { display: flex; gap: 2px; }
.win-btn {
  width: 28px;
  height: 28px;
  display: grid;
  place-items: center;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
  transition: background 0.12s var(--ease), color 0.12s var(--ease);
}
.win-btn svg { width: 14px; height: 14px; }
.win-btn:hover { background: var(--surface-2); color: var(--text-0); }
.win-btn-close:hover { background: var(--accent-red); color: #1a0508; }

.win-body {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  color: var(--text-0);
}

/* 八方向缩放柄：贴边热区 + hover 高亮（Kali 蓝），四角比边更宽以易命中 */
.win-rz { position: absolute; z-index: 10; }
.win-rz:hover { background: rgba(39, 127, 255, 0.25); }
.rz-n { left: 10px; right: 10px; top: 0; height: 5px; cursor: ns-resize; }
.rz-s { left: 10px; right: 10px; bottom: 0; height: 5px; cursor: ns-resize; }
.rz-e { top: 10px; bottom: 10px; right: 0; width: 5px; cursor: ew-resize; }
.rz-w { top: 10px; bottom: 10px; left: 0; width: 5px; cursor: ew-resize; }
.rz-ne { right: 0; top: 0; width: 12px; height: 12px; cursor: nesw-resize; border-top-right-radius: var(--win-radius); }
.rz-nw { left: 0; top: 0; width: 12px; height: 12px; cursor: nwse-resize; border-top-left-radius: var(--win-radius); }
.rz-se { right: 0; bottom: 0; width: 12px; height: 12px; cursor: nwse-resize; border-bottom-right-radius: var(--win-radius); }
.rz-sw { left: 0; bottom: 0; width: 12px; height: 12px; cursor: nesw-resize; border-bottom-left-radius: var(--win-radius); }
</style>
