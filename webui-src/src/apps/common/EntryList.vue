<script setup>
import EntryControl from '../config/EntryControl.vue';
import { splitDesc } from '../config/configUtils.js';

/**
 * 配置字段行列表：Config（功能段）与 Automation（模块）共用。
 * 描述统一过 splitDesc——Enabled 项描述头里的 Author:/Side: 元数据在此拆掉，
 * 普通描述不受影响（仅去掉首尾空行）。
 */
const props = defineProps({
  entries: { type: Array, required: true },
});
const emit = defineEmits(['commit']);

function descOf(e) {
  return splitDesc(e.description).text;
}
</script>

<template>
  <div class="entry-list">
    <div v-for="e in entries" :key="e.key" class="field-row">
      <div class="field-meta">
        <span class="field-key">{{ e.key }}</span>
        <span v-if="descOf(e)" class="field-desc">{{ descOf(e) }}</span>
      </div>
      <div class="field-control">
        <EntryControl :entry="e" @commit="v => emit('commit', e, v)" />
      </div>
    </div>
    <div v-if="!entries.length" class="empty">无匹配字段。</div>
  </div>
</template>

<style scoped>
.entry-list { padding: 6px 16px 20px; }
.field-row {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 11px 0;
  border-bottom: 1px solid var(--line);
}
.field-meta { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
.field-key { font-family: var(--font-mono); font-size: 12.5px; color: var(--text-0); }
.field-desc { font-size: 11.5px; color: var(--text-2); line-height: 1.5; white-space: pre-wrap; }
.field-control { flex-shrink: 0; min-width: 160px; display: flex; justify-content: flex-end; }
.empty { text-align: center; color: var(--text-2); padding: 40px 0; font-size: 12.5px; }
</style>
