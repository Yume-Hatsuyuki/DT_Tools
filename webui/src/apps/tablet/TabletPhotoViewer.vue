<script setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue';
import { t } from '../../i18n/index.js';
import IconX from '~icons/tabler/x';
import IconChevronLeft from '~icons/tabler/chevron-left';
import IconChevronRight from '~icons/tabler/chevron-right';
import IconZoomIn from '~icons/tabler/zoom-in';
import IconZoomOut from '~icons/tabler/zoom-out';
import IconMaximize from '~icons/tabler/maximize';
import IconZoomReset from '~icons/tabler/zoom-reset';
import IconCopy from '~icons/tabler/copy';
import IconDownload from '~icons/tabler/download';
import {
  initialView, zoomAt, toggleView, clampPan, wheelFactor, pinchMetrics,
} from './tabletViewer.js';

const props = defineProps({
  photos: { type: Array, default: () => [] },
  currentKey: { type: String, default: '' },
});
const emit = defineEmits(['select', 'close', 'copy', 'save']);

const rootEl = ref(null);
const stageEl = ref(null);
const imgEl = ref(null);
const natural = ref({ w: 0, h: 0 });
const vp = ref({ w: 0, h: 0 });
const view = ref({ scale: 1, tx: 0, ty: 0 });

const index = computed(() => {
  const i = props.photos.findIndex((p) => p.key === props.currentKey);
  return i < 0 ? 0 : i;
});
const current = computed(() => props.photos[index.value] || null);
const geom = computed(() => ({ iw: natural.value.w, ih: natural.value.h, vw: vp.value.w, vh: vp.value.h }));
const pct = computed(() => Math.round(view.value.scale * 100));
const imgStyle = computed(() => ({
  width: natural.value.w ? `${natural.value.w}px` : 'auto',
  height: natural.value.h ? `${natural.value.h}px` : 'auto',
  transform: `translate(calc(-50% + ${view.value.tx}px), calc(-50% + ${view.value.ty}px)) scale(${view.value.scale})`,
}));

function measure() {
  const el = stageEl.value;
  if (el) vp.value = { w: el.clientWidth, h: el.clientHeight };
}

function relCenter(clientX, clientY) {
  const r = stageEl.value.getBoundingClientRect();
  return { cx: clientX - (r.left + r.width / 2), cy: clientY - (r.top + r.height / 2) };
}

function onLoad() {
  natural.value = { w: imgEl.value.naturalWidth, h: imgEl.value.naturalHeight };
  measure();
  view.value = initialView(geom.value);
}

watch(() => current.value?.key, async () => {
  natural.value = { w: 0, h: 0 };
  view.value = { scale: 1, tx: 0, ty: 0 };
  await nextTick();
  measure();
});

function onResize() {
  measure();
  if (natural.value.w) view.value = initialView(geom.value);
}

function onWheel(e) {
  e.preventDefault();
  const { cx, cy } = relCenter(e.clientX, e.clientY);
  view.value = zoomAt(view.value, wheelFactor(e.deltaY), cx, cy, geom.value);
}
function onDblClick(e) {
  const { cx, cy } = relCenter(e.clientX, e.clientY);
  view.value = toggleView(view.value, cx, cy, geom.value);
}

const pointers = new Map();
let drag = null;
let pinch = null;

function onPointerDown(e) {
  if (e.pointerType === 'mouse' && e.button !== 0) return;
  const fromBg = e.target === stageEl.value;
  stageEl.value.setPointerCapture?.(e.pointerId);
  pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
  if (pointers.size === 1) {
    drag = { x: e.clientX, y: e.clientY, tx: view.value.tx, ty: view.value.ty, moved: false, fromBg };
  } else if (pointers.size === 2) {
    const [a, b] = [...pointers.values()];
    pinch = { dist0: pinchMetrics(a, b).dist, view0: { ...view.value } };
    drag = null;
  }
}

function onPointerMove(e) {
  if (!pointers.has(e.pointerId)) return;
  pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
  if (pinch && pointers.size >= 2) {
    const [a, b] = [...pointers.values()];
    const m = pinchMetrics(a, b);
    if (pinch.dist0 > 0) {
      const { cx, cy } = relCenter(m.cx, m.cy);
      view.value = zoomAt(pinch.view0, m.dist / pinch.dist0, cx, cy, geom.value);
    }
    return;
  }
  if (drag && pointers.size === 1) {
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    if (Math.abs(dx) + Math.abs(dy) > 4) drag.moved = true;
    view.value = { ...view.value, ...clampPan(drag.tx + dx, drag.ty + dy, view.value.scale, geom.value) };
  }
}

function onPointerUp(e) {
  if (!pointers.has(e.pointerId)) return;
  const finished = drag;
  pointers.delete(e.pointerId);
  if (pointers.size < 2) pinch = null;
  if (pointers.size === 1 && !drag) {
    const p = [...pointers.values()][0];
    drag = { x: p.x, y: p.y, tx: view.value.tx, ty: view.value.ty, moved: true, fromBg: false };
  }
  if (pointers.size === 0) {
    if (finished && finished.fromBg && !finished.moved) emit('close');
    drag = null;
  }
}

function onPointerCancel(e) {
  pointers.delete(e.pointerId);
  pinch = null;
  if (pointers.size === 0) drag = null;
}

function zoomBy(factor) {
  view.value = zoomAt(view.value, factor, 0, 0, geom.value);
}
function fit() {
  view.value = initialView(geom.value);
}
function actual() {
  view.value = zoomAt(view.value, 1 / view.value.scale, 0, 0, geom.value);
}
function step(delta) {
  const next = props.photos[index.value + delta];
  if (next) emit('select', next.key);
}

function onKey(e) {
  if (e.key === 'Escape') {
    e.preventDefault();
    emit('close');
  } else if (e.key === 'ArrowLeft') {
    step(-1);
  } else if (e.key === 'ArrowRight') {
    step(1);
  } else if (e.key === '+' || e.key === '=') {
    zoomBy(1.25);
  } else if (e.key === '-') {
    zoomBy(1 / 1.25);
  } else if (e.key === '0') {
    fit();
  } else if (e.key === '1') {
    actual();
  }
}

onMounted(() => {
  measure();
  stageEl.value.addEventListener('wheel', onWheel, { passive: false });
  window.addEventListener('resize', onResize);
  rootEl.value?.focus({ preventScroll: true });
});
onUnmounted(() => {
  stageEl.value?.removeEventListener('wheel', onWheel);
  window.removeEventListener('resize', onResize);
});
</script>

<template>
  <div ref="rootEl" class="viewer" role="dialog" aria-modal="true" :aria-label="t('tablet.viewer.label')" tabindex="-1" @keydown="onKey">
    <header class="v-top">
      <div v-if="current && current.name" class="v-meta">
        <span class="v-name">{{ current.name }}</span>
        <span class="v-time">{{ current.time }}</span>
      </div>
      <span v-if="photos.length > 1" class="v-count">{{ index + 1 }} / {{ photos.length }}</span>
      <button type="button" class="v-btn" :title="t('common.close')" :aria-label="t('common.close')" @click="emit('close')">
        <IconX />
      </button>
    </header>

    <div
      ref="stageEl"
      class="v-stage"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerCancel"
      @dblclick="onDblClick"
    >
      <img
        v-if="current"
        ref="imgEl"
        :key="current.key"
        :src="current.src"
        :alt="t('tablet.photo.alt')"
        :style="imgStyle"
        draggable="false"
        @load="onLoad"
      />
    </div>

    <button
      v-if="index > 0"
      type="button"
      class="v-nav prev"
      :title="t('tablet.viewer.prev')"
      :aria-label="t('tablet.viewer.prev')"
      @click="step(-1)"
    ><IconChevronLeft /></button>
    <button
      v-if="index < photos.length - 1"
      type="button"
      class="v-nav next"
      :title="t('tablet.viewer.next')"
      :aria-label="t('tablet.viewer.next')"
      @click="step(1)"
    ><IconChevronRight /></button>

    <footer class="v-bar">
      <button type="button" class="v-btn" :title="t('tablet.viewer.zoomOut')" :aria-label="t('tablet.viewer.zoomOut')" @click="zoomBy(1 / 1.25)">
        <IconZoomOut />
      </button>
      <span class="v-pct">{{ pct }}%</span>
      <button type="button" class="v-btn" :title="t('tablet.viewer.zoomIn')" :aria-label="t('tablet.viewer.zoomIn')" @click="zoomBy(1.25)">
        <IconZoomIn />
      </button>
      <button type="button" class="v-txt" @click="fit"><IconMaximize /> {{ t('tablet.viewer.fit') }}</button>
      <button type="button" class="v-txt" @click="actual"><IconZoomReset /> {{ t('tablet.viewer.actual') }}</button>
      <template v-if="current && current.line">
        <span class="v-sep"></span>
        <button type="button" class="v-txt" @click="emit('copy', current.line)"><IconCopy /> {{ t('tablet.photo.copy') }}</button>
        <button type="button" class="v-txt" @click="emit('save', current.line)"><IconDownload /> {{ t('tablet.photo.saveAs') }}</button>
      </template>
    </footer>
  </div>
</template>

<style scoped>
.viewer { position: absolute; inset: 0; z-index: 40; display: flex; flex-direction: column; background: rgba(4, 8, 14, 0.94); color: var(--text-1); outline: none; }
.v-top { display: flex; align-items: center; gap: 10px; padding: 8px 12px; min-height: 40px; box-sizing: border-box; }
.v-meta { flex: 1; min-width: 0; display: flex; align-items: baseline; gap: 8px; overflow: hidden; }
.v-name { font-size: 12px; color: var(--text-0); font-weight: 500; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.v-time { font-family: var(--font-mono); font-size: 10.5px; color: var(--text-2); white-space: nowrap; }
.v-count { margin-left: auto; font-family: var(--font-mono); font-size: 11px; color: var(--text-2); }
.v-stage { flex: 1; min-height: 0; position: relative; overflow: hidden; touch-action: none; user-select: none; }
.v-stage img { position: absolute; left: 50%; top: 50%; max-width: none; max-height: none; transform-origin: 50% 50%; will-change: transform; border-radius: 6px; }
.v-btn, .v-txt { display: inline-flex; align-items: center; gap: 5px; background: rgba(255, 255, 255, 0.08); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-1); cursor: pointer; }
.v-btn { width: 30px; height: 30px; justify-content: center; padding: 0; flex: none; }
.v-txt { font-size: 11.5px; padding: 5px 10px; }
.v-btn:hover, .v-txt:hover, .v-nav:hover { color: var(--text-0); border-color: var(--line-strong); }
.v-btn:focus-visible, .v-txt:focus-visible, .v-nav:focus-visible { outline: 2px solid var(--accent-cyan); outline-offset: 2px; }
.v-nav { position: absolute; top: 50%; transform: translateY(-50%); width: 40px; height: 64px; display: inline-flex; align-items: center; justify-content: center; background: rgba(0, 0, 0, 0.35); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-1); cursor: pointer; }
.v-nav.prev { left: 8px; }
.v-nav.next { right: 8px; }
.v-bar { display: flex; align-items: center; justify-content: center; gap: 8px; flex-wrap: wrap; padding: 8px 12px; box-sizing: border-box; }
.v-pct { min-width: 46px; text-align: center; font-family: var(--font-mono); font-size: 11.5px; color: var(--text-2); }
.v-sep { width: 1px; height: 18px; background: var(--line); margin: 0 4px; }
</style>
