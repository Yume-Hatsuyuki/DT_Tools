<script setup>
import { ref, computed, onMounted } from 'vue';
import { t } from '../../i18n/index.js';

const props = defineProps({
  src: { type: String, required: true },
  canSend: { type: Boolean, default: false },
  waitSec: { type: Number, default: 0 },
  busy: { type: Boolean, default: false },
});
const emit = defineEmits(['cancel', 'confirm', 'zoom']);

const cancelBtn = ref(null);

const sendText = computed(() => {
  if (props.busy) return t('tablet.photo.sending');
  if (props.waitSec > 0) return t('tablet.photo.waitSec', { n: props.waitSec });
  return t('tablet.photo.send');
});
const sendDisabled = computed(() => props.busy || !props.canSend || props.waitSec > 0);

function onKey(e) {
  if (e.key === 'Escape') {
    e.preventDefault();
    emit('cancel');
  }
}

onMounted(() => cancelBtn.value?.focus());
</script>

<template>
  <div class="mask" @click.self="emit('cancel')">
    <div class="dialog" role="dialog" aria-modal="true" :aria-label="t('tablet.photo.confirmTitle')" @keydown="onKey">
      <header class="dhead">
        <span class="dtitle">{{ t('tablet.photo.confirmTitle') }}</span>
      </header>
      <div class="preview">
        <img
          :src="src"
          :alt="t('tablet.photo.alt')"
          :title="t('tablet.photo.zoom')"
          draggable="false"
          @click="emit('zoom')"
        />
      </div>
      <p class="hint">{{ t('tablet.photo.confirmHint') }}</p>
      <p v-if="!canSend" class="hint warn">{{ t('tablet.photo.notAllowed') }}</p>
      <footer class="dfoot">
        <button ref="cancelBtn" type="button" class="tb-btn" @click="emit('cancel')">{{ t('common.cancel') }}</button>
        <button type="button" class="tb-btn primary" :disabled="sendDisabled" @click="emit('confirm')">{{ sendText }}</button>
      </footer>
    </div>
  </div>
</template>

<style scoped>
.mask { position: absolute; inset: 0; z-index: 30; background: rgba(4, 8, 14, 0.55); display: flex; align-items: center; justify-content: center; padding: 16px; box-sizing: border-box; }
.dialog { width: min(480px, 100%); max-height: 100%; display: flex; flex-direction: column; box-sizing: border-box; background: var(--surface-1); border: 1px solid var(--line-strong); border-radius: var(--radius-md); box-shadow: var(--menu-shadow, 0 12px 32px rgba(0, 0, 0, 0.45)); overflow: hidden; }
.dhead { padding: 12px 14px; border-bottom: 1px solid var(--line); }
.dtitle { font-size: 13.5px; color: var(--text-0); font-weight: 600; }
.preview { flex: 1; min-height: 120px; display: flex; align-items: center; justify-content: center; padding: 12px; box-sizing: border-box; background: rgba(0, 0, 0, 0.25); overflow: hidden; }
.preview img { display: block; max-width: 100%; max-height: 46vh; object-fit: contain; border-radius: 8px; cursor: zoom-in; }
.hint { margin: 0; padding: 8px 14px 0; font-size: 12px; color: var(--text-2); }
.hint.warn { color: var(--accent-amber); }
.dfoot { display: flex; justify-content: flex-end; gap: 8px; padding: 10px 14px; border-top: 1px solid var(--line); }
.tb-btn { background: var(--surface-2); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-1); font-size: 11.5px; padding: 6px 12px; cursor: pointer; }
.tb-btn:disabled { opacity: 0.45; cursor: default; }
.tb-btn.primary { background: var(--accent-blue-btn); border-color: transparent; color: #fff; }
.tb-btn:focus-visible { outline: 2px solid var(--accent-cyan); outline-offset: 2px; }
</style>
