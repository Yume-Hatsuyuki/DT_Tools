<script setup>
import { ref, computed } from 'vue';
import { API } from '../../api.js';
import { useSectionMeta } from '../../composables/useSectionMeta.js';
import { sectionMeta } from './configUtils.js';
import EntryList from '../common/EntryList.vue';
import LogPanel from '../common/LogPanel.vue';
import IconArrowLeft from '~icons/tabler/arrow-left';
import IconRotateClockwise2 from '~icons/tabler/rotate-clockwise-2';
import IconHistory from '~icons/tabler/history';

const props = defineProps({
  section: { type: Object, required: true },
});
const emit = defineEmits(['back', 'updated', 'reset', 'toast']);

const meta = computed(() => sectionMeta(props.section));
const sectionMetaStore = useSectionMeta();
const fieldQuery = ref('');
const filteredEntries = computed(() => {
  const q = fieldQuery.value.trim().toLowerCase();
  const entries = props.section.entries || [];
  if (!q) return entries;
  return entries.filter(e =>
    (e.key || '').toLowerCase().includes(q) || (e.description || '').toLowerCase().includes(q));
});

/** 备忘录级显示名（WebUI 层，不触配置）。 */
const alias = computed(() => sectionMetaStore.displayName(props.section.section));

async function commitEntry(entry, value) {
  const j = await API.configUpdate(props.section.section, entry.key, value);
  if (j.unauthorized) return;
  if (!j.ok) { emit('toast', { text: j.error || '更新失败', error: true }); return; }
  entry.value = j.value != null ? j.value : value;
  emit('updated');
  emit('toast', { text: `${props.section.section}.${entry.key} 已更新` });
}

async function resetSection() {
  if (!confirm(`恢复段 [${props.section.section}] 全部默认值？（仅做临时调整，如需持久化请使用保存功能。）`)) return;
  const j = await API.configReset(props.section.section);
  if (j.unauthorized) return;
  // 通知父级 reload：详情页打开期间自动刷新被 isEditing 抑制，不主动刷就一直是旧值
  if (j.ok) { emit('reset'); emit('toast', { text: `已重置 ${j.reset} 项` }); }
  else emit('toast', { text: j.error || '重置失败', error: true });
}

const logOpen = ref(false);
</script>

<template>
  <div class="section-detail">
    <div class="detail-header">
      <button class="back-btn" @click="emit('back')"><IconArrowLeft /> 全部配置</button>
      <div class="detail-title">
        <span class="bracket-name" :title="section.section">[{{ alias || section.section }}]</span>
        <span v-if="meta.side" class="badge">{{ meta.side }}</span>
        <span v-if="meta.author" class="author">by {{ meta.author }}</span>
      </div>
      <div class="detail-actions">
        <input v-model="fieldQuery" class="field-search" type="search" placeholder="搜索字段…">
        <button class="icon-btn" :class="{ active: logOpen }" title="本段日志" @click="logOpen = !logOpen"><IconHistory /></button>
        <button class="icon-btn" title="恢复本段默认" @click="resetSection"><IconRotateClockwise2 /></button>
      </div>
    </div>

    <div v-if="logOpen" class="section-log">
      <LogPanel
        :fetch="() => API.sectionLog(props.section.section)"
        :clear="async () => { await API.sectionLogClear(props.section.section); }"
      />
    </div>

    <div class="fields">
      <EntryList :entries="filteredEntries" @commit="commitEntry" />
    </div>
  </div>
</template>

<style scoped>
.section-detail { display: flex; flex-direction: column; height: 100%; background: var(--surface-0); }

.detail-header {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--line);
  flex-shrink: 0;
  flex-wrap: wrap;
}
.back-btn {
  display: flex; align-items: center; gap: 6px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 6px 11px;
  cursor: pointer;
  flex-shrink: 0;
}
.back-btn:hover { color: var(--text-0); }
.back-btn svg { width: 13px; height: 13px; }

.detail-title { display: flex; align-items: baseline; gap: 8px; flex: 1; min-width: 0; }
.bracket-name { font-family: var(--font-mono); font-size: 14px; font-weight: 600; color: var(--accent-cyan); }
.badge {
  font-size: 10.5px;
  color: var(--text-2);
  border: 1px solid var(--line-strong);
  border-radius: 4px;
  padding: 1px 6px;
}
.author { font-size: 11.5px; color: var(--text-2); }

.detail-actions { display: flex; align-items: center; gap: 6px; }
.field-search {
  background: var(--surface-1);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12px;
  padding: 6px 10px;
  width: 160px;
  outline: none;
}
.icon-btn {
  width: 30px; height: 30px;
  display: grid; place-items: center;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.icon-btn:hover, .icon-btn.active { color: var(--text-0); border-color: var(--line-strong); }
.icon-btn.active { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.icon-btn svg { width: 14px; height: 14px; }

.section-log { padding: 10px 16px; border-bottom: 1px solid var(--line); background: var(--surface-1); }

.fields { flex: 1; overflow-y: auto; }
</style>
