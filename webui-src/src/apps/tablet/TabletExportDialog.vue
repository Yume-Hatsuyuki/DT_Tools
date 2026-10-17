<script setup>
import { ref, computed } from 'vue';
import { chatSummary, clueId, clueSummary } from './tabletFormat.js';

/**
 * 导出前的选择框：逐条打勾（默认全选），支持全选 / 全不选。确认后只返回选中的条目，保持原顺序。
 */
const props = defineProps({
  kind: { type: String, required: true },   // 'chat' | 'clue'
  format: { type: String, required: true }, // 'text' | 'image'
  items: { type: Array, default: () => [] },
});
const emit = defineEmits(['cancel', 'confirm']);

function keyOf(item) {
  return props.kind === 'chat' ? item.seq : clueId(item);
}

/**
 * 选中状态只记录被勾选的 key；计数与全选框一律按当前可见的条目计算。
 * 聊天栏满员时最旧的条目会被挤掉，若直接用 Set 的大小计数，会把已不存在的 key 算进去。
 */
const selected = ref(new Set(props.items.map(keyOf)));
const count = computed(() => props.items.reduce((n, i) => n + (selected.value.has(keyOf(i)) ? 1 : 0), 0));
const allChecked = computed(() => props.items.length > 0 && count.value === props.items.length);
const title = computed(() => `导出${props.kind === 'chat' ? '聊天记录' : '线索'}（${props.format === 'text' ? '文本' : '图片'}）`);

function isChecked(item) {
  return selected.value.has(keyOf(item));
}

function toggle(item) {
  const next = new Set(selected.value);
  const k = keyOf(item);
  if (next.has(k)) next.delete(k);
  else next.add(k);
  selected.value = next;
}

function toggleAll() {
  selected.value = allChecked.value ? new Set() : new Set(props.items.map(keyOf));
}

function summary(item) {
  return props.kind === 'chat' ? chatSummary(item) : clueSummary(item);
}

function confirm() {
  emit('confirm', props.items.filter((i) => selected.value.has(keyOf(i))));
}
</script>

<template>
  <div class="mask" @click.self="emit('cancel')">
    <div class="dialog" role="dialog" aria-modal="true">
      <header class="dhead">
        <span class="dtitle">{{ title }}</span>
        <span class="dcount">已选 {{ count }} / {{ items.length }}</span>
      </header>

      <div class="dbar">
        <label class="check-all">
          <input type="checkbox" :checked="allChecked" :disabled="!items.length" @change="toggleAll" />
          <span>全选</span>
        </label>
      </div>

      <div class="dlist">
        <div v-if="!items.length" class="empty">没有可导出的内容</div>
        <label v-for="item in items" :key="keyOf(item)" class="row" :class="{ on: isChecked(item) }">
          <input type="checkbox" :checked="isChecked(item)" @change="toggle(item)" />
          <span class="row-text">{{ summary(item) }}</span>
        </label>
      </div>

      <footer class="dfoot">
        <button class="tb-btn" @click="emit('cancel')">取消</button>
        <button class="tb-btn primary" :disabled="!count" @click="confirm">导出 {{ count }} 条</button>
      </footer>
    </div>
  </div>
</template>

<style scoped>
.mask { position: absolute; inset: 0; z-index: 30; background: rgba(4, 8, 14, 0.55); display: flex; align-items: center; justify-content: center; padding: 16px; box-sizing: border-box; }
.dialog { width: min(520px, 100%); max-height: 100%; display: flex; flex-direction: column; background: var(--surface-1); border: 1px solid var(--line-strong); border-radius: var(--radius-md); box-shadow: var(--menu-shadow); overflow: hidden; }
.dhead { display: flex; align-items: center; justify-content: space-between; padding: 12px 14px; border-bottom: 1px solid var(--line); }
.dtitle { font-size: 13.5px; color: var(--text-0); font-weight: 600; }
.dcount { font-size: 11.5px; color: var(--text-2); }
.dbar { padding: 8px 14px; border-bottom: 1px solid var(--line); }
.check-all, .row { display: flex; align-items: center; gap: 9px; font-size: 12.5px; color: var(--text-1); cursor: pointer; }
.dlist { flex: 1; min-height: 120px; overflow-y: auto; padding: 4px 0; }
.row { padding: 6px 14px; }
.row:hover { background: rgba(140, 180, 255, 0.05); }
.row.on { color: var(--text-0); }
.row-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
input[type="checkbox"] { accent-color: var(--accent-cyan); flex: none; }
.empty { text-align: center; font-size: 12px; color: var(--text-2); padding: 24px 0; }
.dfoot { display: flex; justify-content: flex-end; gap: 8px; padding: 10px 14px; border-top: 1px solid var(--line); }
.tb-btn { background: var(--surface-2); border: 1px solid var(--line); border-radius: var(--radius-sm); color: var(--text-1); font-size: 11.5px; padding: 6px 12px; cursor: pointer; }
.tb-btn:disabled { opacity: 0.45; cursor: default; }
.tb-btn.primary { background: var(--accent-blue-btn); border-color: transparent; color: #fff; }
</style>
