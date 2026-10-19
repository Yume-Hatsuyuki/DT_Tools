<script setup>
import { computed } from 'vue';
import { t, LOCALES, localePref, setLocalePref, locale } from '../i18n/index.js';
import IconX from '~icons/tabler/x';
import IconCheck from '~icons/tabler/check';

/**
 * 语言弹窗：界面语言。默认「跟随浏览器」；选择某个语言后立即生效并保存在本浏览器。
 * 语言切换是响应式的，整个界面（包括本弹窗）会即时刷新。
 */
const emit = defineEmits(['close']);

const pref = computed(() => localePref());
const current = computed(() => LOCALES.find((l) => l.code === locale.value));

const options = computed(() => [
  {
    code: 'auto',
    name: t('settings.followBrowser'),
    sub: t('settings.followBrowserHint', { lang: current.value ? current.value.name : '' }),
  },
  ...LOCALES.map((l) => ({ code: l.code, name: l.name, sub: '' })),
]);

function pick(code) {
  setLocalePref(code);
}
</script>

<template>
  <div class="sm-mask" @click.self="emit('close')">
    <div class="sm-panel" role="dialog" aria-modal="true" :aria-label="t('common.language')">
      <div class="sm-head">
        <span class="sm-title">{{ t('common.language') }}</span>
        <button class="sm-x" :title="t('common.close')" @click="emit('close')"><IconX /></button>
      </div>
      <ul class="sm-list">
        <li v-for="o in options" :key="o.code">
          <button type="button" class="sm-opt" :class="{ on: pref === o.code }" @click="pick(o.code)">
            <span class="sm-name">{{ o.name }}</span>
            <span v-if="o.sub" class="sm-sub">{{ o.sub }}</span>
            <IconCheck v-if="pref === o.code" class="sm-check" />
          </button>
        </li>
      </ul>
      <div class="sm-hint">{{ t('settings.storeHint') }}</div>
    </div>
  </div>
</template>

<style scoped>
.sm-mask {
  position: fixed;
  inset: 0;
  z-index: 4700;
  background: rgba(4, 8, 14, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  box-sizing: border-box;
}
.sm-panel {
  width: 300px;
  max-width: 100%;
  padding: 14px 14px 12px;
  border-radius: 14px;
  background: rgba(9, 18, 26, 0.96);
  backdrop-filter: blur(20px) saturate(150%);
  border: 1px solid var(--dock-border);
  box-shadow: 0 20px 50px -12px rgba(2, 10, 18, 0.8);
  color: var(--text-1);
}
.sm-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px; }
.sm-title { font-size: 14px; font-weight: 600; color: var(--text-0); }
.sm-x {
  width: 24px;
  height: 24px;
  display: grid;
  place-items: center;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
}
.sm-x:hover { background: rgba(140, 180, 255, 0.12); color: var(--text-0); }
.sm-x svg { width: 13px; height: 13px; }
.sm-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
.sm-opt {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 10px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  cursor: pointer;
}
.sm-opt:hover { border-color: var(--line-strong); color: var(--text-0); }
.sm-opt.on { border-color: var(--accent-cyan-dim); color: var(--text-0); background: rgba(140, 180, 255, 0.08); }
.sm-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.sm-sub { font-size: 10.5px; color: var(--text-2); }
.sm-check { width: 14px; height: 14px; flex: none; color: var(--accent-cyan); }
.sm-hint { font-size: 10.5px; color: var(--text-2); margin-top: 10px; line-height: 1.5; }
</style>
