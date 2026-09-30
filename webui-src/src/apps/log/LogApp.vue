<script setup>
import { ref, computed, nextTick, onUnmounted } from 'vue';
import { useLogStream } from '../../composables/useLogStream.js';

/**
 * 日志应用（全量日志唯一归口）：控制台只显示各自会话的命令输出后，
 * 这里收全部日志（含全局与各会话）。级别筛选 + 文本搜索 + 暂停跟随 +
 * 清屏（只清本地视图，不动服务端环形缓冲）。
 */
const LEVEL_COLOR = {
  WARN: 'var(--accent-amber)',
  ERROR: 'var(--accent-red)',
  FATAL: 'var(--accent-red)',
  DEBUG: 'var(--text-2)',
};

let uid = 0;
const entries = ref([]);
const MAX_ENTRIES = 5000;
const TRIM_STEP = 1000;

const levelFilter = ref('ALL');   // ALL / INFO / WARN / ERROR
const query = ref('');
const paused = ref(false);        // 暂停后新条目丢弃不入视图（再次点击恢复）

function pass(e) {
  if (paused.value) return false;
  if (levelFilter.value === 'WARN' && e.level !== 'WARN' && e.level !== 'ERROR' && e.level !== 'FATAL') return false;
  if (levelFilter.value === 'ERROR' && e.level !== 'ERROR' && e.level !== 'FATAL') return false;
  if (levelFilter.value === 'INFO' && e.level !== 'INFO' && e.level !== 'DEBUG') return false;
  return true;
}

const stream = useLogStream();
function onStreamEntry(e) {
  if (!pass(e)) return;
  entries.value.push(e);
  if (entries.value.length > MAX_ENTRIES) entries.value.splice(0, TRIM_STEP);
  maybeScroll();
}
stream.replay(onStreamEntry);   // 补齐窗口打开前的日志
onUnmounted(stream.onEntry(onStreamEntry));

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase();
  if (!q) return entries.value;
  return entries.value.filter(e =>
    e.msg.toLowerCase().includes(q) || (e.tag || '').toLowerCase().includes(q));
});

// ── 吸底滚动（与控制台同一套判断：贴底且无选区才跟随）──

const logEl = ref(null);
const stick = ref(true);

function onScroll() {
  const el = logEl.value;
  stick.value = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
}
function selecting() {
  const sel = document.getSelection();
  return !!sel && !sel.isCollapsed && logEl.value && logEl.value.contains(sel.anchorNode);
}
async function maybeScroll() {
  if (!stick.value || selecting()) return;
  await nextTick();
  if (stick.value && logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight;
}

function clearView() {
  entries.value = [];
}
</script>

<template>
  <div class="logapp">
    <div class="toolbar">
      <div class="chip-row">
        <button
          v-for="lv in ['ALL', 'INFO', 'WARN', 'ERROR']"
          :key="lv"
          class="chip"
          :class="{ active: levelFilter === lv }"
          @click="levelFilter = lv"
        >{{ lv }}</button>
      </div>
      <input v-model="query" class="search" type="search" placeholder="搜索日志…">
      <button class="tb-btn" :class="{ active: !paused }" :title="paused ? '已暂停：新日志不入视图' : '跟随中'" @click="paused = !paused">
        {{ paused ? '▶ 恢复' : '⏸ 暂停' }}
      </button>
      <button class="tb-btn" @click="clearView">清屏</button>
    </div>

    <div ref="logEl" class="log" @scroll="onScroll">
      <div v-for="e in filtered" :key="e.id" class="entry">
        <span class="ts">{{ e.time }}</span>
        <span class="tg">[{{ e.tag }}]</span>
        <span :style="{ color: LEVEL_COLOR[e.level] || 'var(--text-0)' }">{{ e.msg }}</span>
      </div>
      <div v-if="!filtered.length" class="empty">（暂无日志）</div>
    </div>

    <div class="foot">
      <span class="count">{{ filtered.length }} 条</span>
      <span v-if="paused" class="paused-mark">已暂停跟随</span>
    </div>
  </div>
</template>

<style scoped>
.logapp { display: flex; flex-direction: column; height: 100%; background: var(--surface-0); }

.toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--line);
  background: var(--surface-1);
  flex-shrink: 0;
  flex-wrap: wrap;
}
.chip-row { display: flex; gap: 4px; }
.chip {
  font-size: 11px;
  padding: 4px 10px;
  border: 1px solid var(--line);
  border-radius: 999px;
  background: var(--surface-2);
  color: var(--text-1);
  cursor: pointer;
  font-family: var(--font-mono);
}
.chip.active { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.search {
  flex: 1;
  min-width: 120px;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12px;
  padding: 5px 10px;
  outline: none;
}
.tb-btn {
  font-size: 11.5px;
  padding: 5px 10px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
  white-space: nowrap;
}
.tb-btn:hover { color: var(--text-0); border-color: var(--line-strong); }
.tb-btn.active { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }

.log {
  flex: 1;
  overflow-y: auto;
  padding: 10px 14px;
  font-family: var(--font-mono);
  font-size: 12px;
  line-height: 1.7;
}
.entry { white-space: pre-wrap; word-break: break-word; }
.ts { color: var(--text-2); margin-right: 8px; }
.tg { color: var(--text-2); margin-right: 6px; }
.empty { color: var(--text-2); text-align: center; padding: 30px 0; }

.foot {
  padding: 5px 12px;
  border-top: 1px solid var(--line);
  background: var(--surface-1);
  font-size: 11px;
  color: var(--text-2);
  display: flex;
  gap: 12px;
  flex-shrink: 0;
}
.paused-mark { color: var(--accent-amber); }
</style>
