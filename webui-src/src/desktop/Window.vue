<script setup>
import { computed, provide } from 'vue';
import IconX from '~icons/tabler/x';
import IconMinus from '~icons/tabler/minus';
import IconPlus from '~icons/tabler/plus';

const props = defineProps({
  win: { type: Object, required: true },
  /** 平铺模式：窗口铺满「顶栏之下、Dock 之上」，不接受拖动/缩放（标签页式的简洁切换）。 */
  tiled: { type: Boolean, default: false },
  /** 平铺模式下是否为当前激活窗口；非激活窗口保持挂载但隐藏，切换标签不丢应用内状态。 */
  active: { type: Boolean, default: true },
});
const emit = defineEmits(['close', 'focus', 'minimize', 'toggle-maximize', 'update-geometry']);

// 给窗口内应用（如终端的 exit 内置命令）一个受控的自我操作通道
// （最小化/最大化走窗口 chrome 与 TopBar 菜单，不经此通道）
provide('winApi', {
  close: () => emit('close'),
});

const MIN_W = 360;
const MIN_H = 240;
// 与 tokens.css --topbar-h 同步：窗口不得拖进/放大到顶栏之下
const TOPBAR_H = 32;

const style = computed(() => {
  // 平铺模式：只让激活窗口占满工作区，其余隐藏但**不卸载** ——
  // 终端历史、输入框草稿这类应用内状态不能因为切一下标签就没了。
  if (props.tiled) {
    if (!props.active) return { display: 'none' };
    return {
      left: '0',
      top: 'var(--topbar-h)',
      width: '100%',
      height: 'calc(100% - var(--topbar-h) - var(--dock-h))',
      zIndex: props.win.z,
    };
  }
  if (props.win.maximized) {
    // macOS 全屏语义近似：盖过 Dock 但给顶栏（菜单栏）让位
    return {
      left: '0',
      top: TOPBAR_H + 'px',
      width: '100%',
      height: `calc(100% - ${TOPBAR_H}px)`,
      zIndex: props.win.z,
    };
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
  if (props.tiled || props.win.maximized) return;
  if (e.target.closest('.traffic')) return;
  const startX = e.clientX, startY = e.clientY;
  const startWinX = props.win.x, startWinY = props.win.y;
  const onMove = (ev) => {
    emit('update-geometry', {
      x: Math.max(0, startWinX + (ev.clientX - startX)),
      y: Math.max(TOPBAR_H, startWinY + (ev.clientY - startY)),
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
  if (props.tiled || props.win.maximized) return;
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
      patch.y = Math.max(TOPBAR_H, s.y + (s.h - h));
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
    :class="{ maximized: win.maximized, tiled: tiled }"
    :style="style"
    @pointerdown="emit('focus')"
  >
    <!-- 平铺模式不显示标题栏：切换交给顶栏标签/Dock（更像浏览器标签页），内容区因此多出一整条高度。
         要关闭应用：右键顶栏标签或 Dock 图标，或切回窗口模式。 -->
    <div v-if="!tiled" class="win-titlebar" @pointerdown="onTitlebarPointerDown" @dblclick="emit('toggle-maximize')">
      <div class="traffic">
        <button class="tl tl-close" title="关闭" @click="emit('close')"><IconX /></button>
        <button class="tl tl-min" title="最小化" @click="emit('minimize')"><IconMinus /></button>
        <button class="tl tl-max" :title="win.maximized ? '还原' : '最大化'" @click="emit('toggle-maximize')">
          <IconPlus />
        </button>
      </div>
      <span class="win-title">{{ win.title }}</span>
      <span class="traffic-spacer" />
    </div>
    <div class="win-body">
      <slot />
    </div>
    <template v-if="!win.maximized && !tiled">
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
  backdrop-filter: blur(22px) saturate(150%);
  overflow: hidden;
  min-width: 360px;
  min-height: 240px;
}
.win.maximized { border-radius: 0; border: none; }
/* 平铺：去掉圆角/阴影/毛玻璃 —— 它此刻就是工作区本身，不是什么浮在上面的窗 */
.win.tiled {
  border-radius: 0;
  border: none;
  box-shadow: none;
  backdrop-filter: none;
  background: var(--surface-0);
  min-width: 0;
  min-height: 0;
}

.win-titlebar {
  position: relative;
  display: flex;
  align-items: center;
  height: 38px;
  padding: 0 12px;
  background: var(--win-titlebar);
  border-bottom: 1px solid var(--line);
  cursor: grab;
  user-select: none;
  flex-shrink: 0;
}
.win:active .win-titlebar { cursor: grabbing; }

/* macOS 交通灯：红关 / 黄最小化 / 绿最大化；悬停亮起符号 */
.traffic {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
  margin-right: 12px;
}
.tl {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  border: none;
  padding: 0;
  display: grid;
  place-items: center;
  cursor: pointer;
  color: transparent;
  transition: filter 0.12s var(--ease), color 0.12s var(--ease);
}
.tl svg { width: 9px; height: 9px; stroke-width: 2.4; }
.tl-close { background: #ff5f57; }
.tl-min { background: #febc2e; }
.tl-max { background: #28c840; }
.traffic:hover .tl { color: rgba(20, 10, 5, 0.65); }
.tl:hover { filter: brightness(1.12); }

.win-title {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  max-width: 55%;
  font-size: 12.5px;
  font-weight: 600;
  letter-spacing: 0.02em;
  color: var(--text-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  pointer-events: none;
}
.traffic-spacer { margin-left: auto; }

.win-body {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  color: var(--text-0);
}

/* 八方向缩放柄：贴边热区 + hover 高亮（壳层高光青），四角比边更宽以易命中 */
.win-rz { position: absolute; z-index: 10; }
.win-rz:hover { background: rgba(95, 217, 246, 0.22); }
.rz-n { left: 10px; right: 10px; top: 0; height: 5px; cursor: ns-resize; }
.rz-s { left: 10px; right: 10px; bottom: 0; height: 5px; cursor: ns-resize; }
.rz-e { top: 10px; bottom: 10px; right: 0; width: 5px; cursor: ew-resize; }
.rz-w { top: 10px; bottom: 10px; left: 0; width: 5px; cursor: ew-resize; }
.rz-ne { right: 0; top: 0; width: 12px; height: 12px; cursor: nesw-resize; border-top-right-radius: var(--win-radius); }
.rz-nw { left: 0; top: 0; width: 12px; height: 12px; cursor: nwse-resize; border-top-left-radius: var(--win-radius); }
.rz-se { right: 0; bottom: 0; width: 12px; height: 12px; cursor: nwse-resize; border-bottom-right-radius: var(--win-radius); }
.rz-sw { left: 0; bottom: 0; width: 12px; height: 12px; cursor: nesw-resize; border-bottom-left-radius: var(--win-radius); }
</style>
