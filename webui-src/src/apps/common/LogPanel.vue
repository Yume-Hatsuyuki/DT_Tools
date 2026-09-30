<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { createPoller } from '../../api.js';

/**
 * 段/模块日志面板：挂载即拉取，3 秒轮询跟随（页面隐藏自动暂停），
 * 关闭即停。数据形状 = 后端 {ok, section, seq, lines:["HH:mm:ss [LEVEL] msg"]}。
 * （旧版按数组解析导致永远显示"无日志"的 BUG 在此修正并集中到这一个组件。）
 */
const props = defineProps({
  /** () => Promise<{ok, lines?}> —— 调用方绑定 sectionLog / moduleLog。 */
  fetch: { type: Function, required: true },
  /** async () => void —— 清空回调（父级负责调 API 并处理错误）。 */
  clear: { type: Function, required: true },
});

const lines = ref([]);
const loadError = ref('');

const LEVEL_COLOR = {
  WARN: 'var(--accent-amber)',
  ERROR: 'var(--accent-red)',
  FATAL: 'var(--accent-red)',
  DEBUG: 'var(--text-2)',
};

const parsed = computed(() =>
  lines.value.map((raw) => {
    const m = String(raw).match(/^(\d{2}:\d{2}:\d{2}) \[(\w+)\] (.*)$/s);
    if (!m) return { time: '', level: '', msg: raw, color: 'var(--text-1)' };
    return { time: m[1], level: m[2], msg: m[3], color: LEVEL_COLOR[m[2]] || 'var(--text-1)' };
  })
);

async function reload() {
  const r = await props.fetch();
  if (r && r.ok && Array.isArray(r.lines)) {
    lines.value = r.lines;
    loadError.value = '';
  } else {
    loadError.value = (r && r.error) || '读取日志失败';
  }
}

async function onClear() {
  await props.clear();
  lines.value = [];
  await reload();
}

let poller = null;
onMounted(() => {
  poller = createPoller(reload, 3000);
  poller.start();
});
onUnmounted(() => poller && poller.stop());
</script>

<template>
  <div class="log-panel">
    <div class="log-lines">
      <div v-for="(l, i) in parsed" :key="i" class="log-line">
        <span v-if="l.time" class="lt">{{ l.time }}</span>
        <span v-if="l.level" class="lv" :style="{ color: l.color }">[{{ l.level }}]</span>
        <span class="lm">{{ l.msg }}</span>
      </div>
      <div v-if="loadError" class="log-empty error">{{ loadError }}</div>
      <div v-else-if="!parsed.length" class="log-empty">（暂无日志）</div>
    </div>
    <div class="log-foot">
      <button type="button" class="btn-xs" @click="onClear">清空日志</button>
    </div>
  </div>
</template>

<style scoped>
.log-panel {
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
}
.log-lines {
  max-height: 160px;
  overflow-y: auto;
  padding: 8px 10px;
  font-family: var(--font-mono);
  font-size: 11px;
  line-height: 1.6;
}
.log-line { white-space: pre-wrap; word-break: break-word; }
.lt { color: var(--text-2); margin-right: 8px; }
.lv { margin-right: 6px; }
.lm { color: var(--text-1); }
.log-empty { color: var(--text-2); }
.log-empty.error { color: var(--accent-red); }
.log-foot { padding: 6px 8px; border-top: 1px solid var(--line); }
.btn-xs {
  font-size: 11px;
  padding: 3px 9px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.btn-xs:hover { color: var(--text-0); border-color: var(--line-strong); }
</style>
