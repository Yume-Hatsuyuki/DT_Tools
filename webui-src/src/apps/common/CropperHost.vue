<script setup>
import { ref, computed } from 'vue';
import { useCropper, cropToDataUrl, clampOffset, STAGE, BOX } from '../../composables/useCropper.js';
import IconX from '~icons/tabler/x';

/** 全局裁切模态框宿主——只在 Desktop 挂载一次，逻辑全在 useCropper。 */
const { state } = useCropper();
const busy = ref(false);

const stageStyle = computed(() => ({ width: STAGE + 'px', height: STAGE + 'px' }));
const boxStyle = computed(() => ({ width: BOX + 'px', height: BOX + 'px' }));

function onWheel(e) {
  e.preventDefault();
  const next = state.scale * (e.deltaY < 0 ? 1.12 : 1 / 1.12);
  state.scale = next;
  clampOffset();
}

function onPointerDown(e) {
  e.preventDefault();
  const startX = e.clientX, startY = e.clientY;
  const ox = state.offsetX, oy = state.offsetY;
  const onMove = (ev) => {
    state.offsetX = ox + (ev.clientX - startX);
    state.offsetY = oy + (ev.clientY - startY);
    clampOffset();
  };
  const onUp = () => {
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
  };
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}

function onSlider(e) {
  state.scale = Number(e.target.value);
  clampOffset();
}

async function confirm() {
  busy.value = true;
  const dataUrl = await cropToDataUrl();
  busy.value = false;
  state.visible = false;
  state.resolve && state.resolve(dataUrl);
  state.resolve = null;
}
function cancel() {
  state.visible = false;
  state.resolve && state.resolve(null);
  state.resolve = null;
}
</script>

<template>
  <div v-if="state.visible" class="crop-mask" @contextmenu.prevent>
    <div class="crop-box">
      <div class="crop-head">
        <span>{{ state.title }}</span>
        <button class="crop-close" type="button" @click="cancel"><IconX /></button>
      </div>
      <div
        class="crop-stage"
        :style="stageStyle"
        @wheel="onWheel"
        @pointerdown="onPointerDown"
        @dragover.prevent
        @drop.prevent
      >
        <div
          class="crop-layer"
          :style="{
            width: state.imgW + 'px',
            height: state.imgH + 'px',
            transform: `translate(-50%, -50%) translate(${state.offsetX}px, ${state.offsetY}px) scale(${state.fit * state.scale})`,
          }"
        >
          <img :src="state.imgSrc" alt="" draggable="false">
        </div>
        <div class="crop-box-vis" :class="{ circle: state.shape === 'circle' }" :style="boxStyle" />
      </div>
      <div class="crop-zoom">
        <span class="zoom-label">缩放</span>
        <input
          type="range"
          min="1"
          max="8"
          step="0.01"
          :value="state.scale"
          @input="onSlider"
        >
        <span class="zoom-value">{{ state.scale.toFixed(2) }}×</span>
      </div>
      <div class="crop-hint">滚轮缩放 · 拖动平移 · 选区内为最终裁切</div>
      <div class="crop-actions">
        <button type="button" @click="cancel">取消</button>
        <button type="button" class="primary" :disabled="busy" @click="confirm">{{ busy ? '处理中…' : '确定' }}</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.crop-mask {
  position: fixed;
  inset: 0;
  z-index: 10001;
  background: rgba(0, 0, 0, 0.55);
  display: grid;
  place-items: center;
}
.crop-box {
  width: 360px;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--win-shadow);
  padding: 12px 14px;
}
.crop-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 13px;
  color: var(--text-0);
  margin-bottom: 10px;
}
.crop-close {
  width: 26px; height: 26px;
  display: grid; place-items: center;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
}
.crop-close:hover { background: var(--surface-2); color: var(--text-0); }
.crop-close svg { width: 14px; height: 14px; }

.crop-stage {
  position: relative;
  margin: 0 auto;
  overflow: hidden;
  background:
    repeating-conic-gradient(#14161f 0% 25%, #0e1018 0% 50%) 0 0 / 20px 20px;
  border: 1px solid var(--line-strong);
  border-radius: var(--radius-sm);
  cursor: grab;
  touch-action: none;
}
.crop-stage:active { cursor: grabbing; }
.crop-layer {
  position: absolute;
  left: 50%;
  top: 50%;
  transform-origin: center center;
  pointer-events: none;
}
.crop-layer img { width: 100%; height: 100%; display: block; user-select: none; }

.crop-box-vis {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  border: 1.5px solid var(--accent-cyan);
  box-shadow: 0 0 0 9999px rgba(5, 7, 12, 0.55);
  pointer-events: none;
}
.crop-box-vis.circle { border-radius: 50%; }

.crop-zoom {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 12px;
}
.zoom-label { font-size: 11.5px; color: var(--text-2); }
.crop-zoom input[type="range"] { flex: 1; accent-color: var(--accent-cyan); }
.zoom-value { font-size: 11.5px; color: var(--text-1); font-family: var(--font-mono); width: 44px; text-align: right; }

.crop-hint { margin-top: 8px; font-size: 11px; color: var(--text-2); text-align: center; }

.crop-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 12px; }
.crop-actions button {
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 6px 14px;
  cursor: pointer;
}
.crop-actions button:hover { color: var(--text-0); }
.crop-actions button.primary {
  background: var(--accent-blue-btn);
  border-color: transparent;
  color: #fff;
}
.crop-actions button.primary:hover { filter: brightness(1.1); color: #fff; }
.crop-actions button:disabled { opacity: 0.6; cursor: default; }
</style>
