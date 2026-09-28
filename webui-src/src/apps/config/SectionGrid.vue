<script setup>
import { computed } from 'vue';
import { sectionMeta } from './configUtils.js';
import IconFolder from '~icons/tabler/folder';
import IconToggleLeft from '~icons/tabler/toggle-left';

const props = defineProps({
  sections: { type: Array, required: true },
  query: { type: String, default: '' },
});
const emit = defineEmits(['open']);

const filtered = computed(() => {
  const q = props.query.trim().toLowerCase();
  if (!q) return props.sections;
  return props.sections.filter(s =>
    s.section.toLowerCase().includes(q) ||
    (s.entries || []).some(e =>
      (e.key || '').toLowerCase().includes(q) || (e.description || '').toLowerCase().includes(q)));
});

function enabledOf(section) {
  const e = (section.entries || []).find(en => en.key === 'Enabled');
  return e ? !!e.value : null;
}
</script>

<template>
  <div class="section-grid">
    <button
      v-for="sec in filtered"
      :key="sec.section"
      class="sec-tile"
      @click="emit('open', sec)"
    >
      <span class="tile-icon">
        <IconFolder />
        <span
          v-if="enabledOf(sec) !== null"
          class="enabled-dot"
          :class="{ on: enabledOf(sec) }"
          :title="enabledOf(sec) ? '已启用' : '已禁用'"
        />
      </span>
      <span class="tile-label">{{ sec.section }}</span>
      <span class="tile-count">{{ (sec.entries || []).length }} 项</span>
    </button>
    <div v-if="!filtered.length" class="empty">无匹配的段。</div>
  </div>
</template>

<style scoped>
.section-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(112px, 1fr));
  gap: 4px;
  padding: 16px;
  overflow-y: auto;
  flex: 1;
  align-content: flex-start;
}

.sec-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 14px 8px 10px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  cursor: pointer;
}
.sec-tile:hover { background: var(--surface-1); border-color: var(--line); }

.tile-icon {
  position: relative;
  width: 44px; height: 44px;
  display: grid; place-items: center;
  border-radius: 10px;
  background: linear-gradient(160deg, rgba(0, 229, 255, 0.08), rgba(255, 45, 149, 0.05));
  border: 1px solid var(--line);
}
.tile-icon svg { width: 22px; height: 22px; color: var(--accent-cyan); }
.enabled-dot {
  position: absolute;
  right: -2px; bottom: -2px;
  width: 10px; height: 10px;
  border-radius: 50%;
  background: var(--text-2);
  border: 2px solid var(--surface-0);
}
.enabled-dot.on { background: var(--accent-green); }

.tile-label {
  font-family: var(--font-mono);
  font-size: 11.5px;
  color: var(--text-1);
  text-align: center;
  word-break: break-word;
  line-height: 1.3;
}
.sec-tile:hover .tile-label { color: var(--text-0); }
.tile-count { font-size: 10px; color: var(--text-2); }

.empty { grid-column: 1 / -1; text-align: center; color: var(--text-2); padding: 40px 0; font-size: 12.5px; }
</style>
