<script setup>
import { ref, computed, nextTick, watch } from 'vue';
import { useConsole } from './useConsole.js';
import JsonBlock from './JsonBlock.vue';

const { entries, commands, send, historyUp, historyDown } = useConsole();

const input = ref('');
const inputEl = ref(null);
const logEl = ref(null);
const selIdx = ref(-1);
const suggestOpen = ref(false);

function getPartial() {
  const m = input.value.match(/^([/!]?)(\S*)$/);
  return m ? { prefix: m[1], partial: m[2].toLowerCase() } : null;
}

const matches = computed(() => {
  const p = getPartial();
  if (p === null) return [];
  return commands.value.filter(c => {
    if (!p.partial) return true;
    if ((c.name || '').toLowerCase().startsWith(p.partial)) return true;
    return (c.aliases || []).some(a => String(a).toLowerCase().startsWith(p.partial));
  });
});

watch(matches, (m) => {
  suggestOpen.value = m.length > 0 && input.value.length > 0;
  if (suggestOpen.value) selIdx.value = 0;
});

function applyMatch(idx) {
  const c = matches.value[idx];
  if (!c) return;
  input.value = '/' + c.name + ' ';
  suggestOpen.value = false;
  inputEl.value && inputEl.value.focus();
}

async function submit() {
  const v = input.value;
  if (!v.trim()) return;
  input.value = '';
  suggestOpen.value = false;
  await send(v);
  await scrollToEnd();
}

async function scrollToEnd() {
  await nextTick();
  if (logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight;
}
watch(entries, scrollToEnd, { deep: false });

function onKeydown(e) {
  if (suggestOpen.value && matches.value.length) {
    if (e.key === 'ArrowDown') { e.preventDefault(); selIdx.value = (selIdx.value + 1) % matches.value.length; return; }
    if (e.key === 'ArrowUp') { e.preventDefault(); selIdx.value = (selIdx.value - 1 + matches.value.length) % matches.value.length; return; }
    if (e.key === 'Tab' || (e.key === 'Enter' && selIdx.value >= 0 && getPartial())) {
      e.preventDefault();
      applyMatch(selIdx.value);
      return;
    }
    if (e.key === 'Escape') { suggestOpen.value = false; return; }
  }
  if (e.key === 'Enter') { e.preventDefault(); submit(); return; }
  if (e.key === 'ArrowUp' && !suggestOpen.value) { e.preventDefault(); input.value = historyUp(input.value); return; }
  if (e.key === 'ArrowDown' && !suggestOpen.value) { e.preventDefault(); input.value = historyDown(); }
}
</script>

<template>
  <div class="console-app">
    <div ref="logEl" class="log">
      <div v-for="e in entries" :key="e.id" class="entry">
        <template v-if="e.json != null">
          <JsonBlock :text="e.json" />
        </template>
        <template v-else>
          <span v-if="e.time" class="ts">{{ e.time }}</span>
          <span :style="{ color: e.color }">{{ e.text }}</span>
        </template>
      </div>
    </div>

    <div class="bar-wrap">
      <div v-if="suggestOpen" class="suggest">
        <div
          v-for="(c, i) in matches"
          :key="c.name"
          class="sug-item"
          :class="{ sel: i === selIdx }"
          @mousedown.prevent="applyMatch(i)"
          @mouseenter="selIdx = i"
        >
          <span class="sug-name">/{{ c.name }}</span>
          <span class="sug-desc">{{ c.description || c.usage || '' }}</span>
          <span v-if="c.author" class="sug-author">功能制作者：{{ c.author }}</span>
        </div>
      </div>
      <div class="bar">
        <input
          ref="inputEl"
          v-model="input"
          placeholder="输入 / 唤出命令补全，↑↓ 翻历史，Enter 执行"
          autocomplete="off"
          spellcheck="false"
          @keydown="onKeydown"
          @blur="suggestOpen = false"
        >
        <button @click="submit">发送</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.console-app { display: flex; flex-direction: column; height: 100%; background: var(--surface-0); }

.log {
  flex: 1;
  overflow-y: auto;
  padding: 12px 14px;
  font-family: var(--font-mono);
  font-size: 12.5px;
  line-height: 1.65;
}
.entry { margin-bottom: 2px; white-space: pre-wrap; word-break: break-word; }
.ts { color: var(--text-2); margin-right: 8px; }

.bar-wrap { position: relative; border-top: 1px solid var(--line); background: var(--surface-1); }

.suggest {
  position: absolute;
  bottom: 100%;
  left: 0; right: 0;
  max-height: 260px;
  overflow-y: auto;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-bottom: none;
}
.sug-item { padding: 7px 12px; cursor: pointer; border-bottom: 1px solid var(--line); }
.sug-item.sel, .sug-item:hover { background: var(--surface-2); }
.sug-name { font-family: var(--font-mono); color: var(--accent-cyan); font-size: 12.5px; margin-right: 8px; }
.sug-desc { font-size: 11.5px; color: var(--text-1); }
.sug-author { display: block; font-size: 10.5px; color: var(--text-2); margin-top: 2px; }

.bar { display: flex; gap: 8px; padding: 10px 12px; }
.bar input {
  flex: 1;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-family: var(--font-mono);
  font-size: 12.5px;
  padding: 9px 11px;
  outline: none;
}
.bar input:focus { border-color: var(--accent-cyan-dim); }
.bar button {
  background: var(--accent-cyan);
  color: #04070c;
  border: none;
  border-radius: var(--radius-sm);
  padding: 0 18px;
  font-size: 12.5px;
  font-weight: 600;
  cursor: pointer;
}
.bar button:hover { filter: brightness(1.1); }
</style>
