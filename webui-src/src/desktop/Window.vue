<script setup>
import { computed, ref } from 'vue';
import IconMinus from '~icons/tabler/minus';
import IconSquare from '~icons/tabler/square';
import IconSquaresDiagonal from '~icons/tabler/squares-diagonal';
import IconX from '~icons/tabler/x';

const props = defineProps({
  win: { type: Object, required: true },
});
const emit = defineEmits(['close', 'focus', 'minimize', 'toggle-maximize', 'update-geometry']);

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
      x: startWinX + (ev.clientX - startX),
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

function onResizePointerDown(e) {
  e.stopPropagation();
  emit('focus');
  const startX = e.clientX, startY = e.clientY;
  const startW = props.win.w, startH = props.win.h;
  const onMove = (ev) => {
    emit('update-geometry', {
      w: Math.max(MIN_W, startW + (ev.clientX - startX)),
      h: Math.max(MIN_H, startH + (ev.clientY - startY)),
    });
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
    <div v-if="!win.maximized" class="win-resize" @pointerdown="onResizePointerDown" />
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

.win-resize {
  position: absolute;
  right: 0;
  bottom: 0;
  width: 16px;
  height: 16px;
  cursor: nwse-resize;
}
.win-resize::after {
  content: '';
  position: absolute;
  right: 4px;
  bottom: 4px;
  width: 7px;
  height: 7px;
  border-right: 2px solid var(--line-strong);
  border-bottom: 2px solid var(--line-strong);
}
</style>
