<script setup>
import { ref, computed, watch, nextTick, onUnmounted } from 'vue';
import { useCommandInput, newSessionId } from './useCommandInput.js';
import { useLogStream } from '../../composables/useLogStream.js';
import SuggestPopup from '../common/SuggestPopup.vue';
import JsonBlock from './JsonBlock.vue';

/**
 * 旧版控制台（/命令 + 发送按钮）。
 * 会话隔离：本窗口只显示自己执行的命令产生的日志（后端按 X-DT-Session 标记
 * 回流），不再打印全部日志——全量日志归「日志」应用，两套控制台互不干扰。
 */
const { commands, historyUp, historyDown, runCommand } = useCommandInput('console');
const mySession = newSessionId();
const LEVEL_COLOR = {
  WARN: 'var(--accent-amber)',
  ERROR: 'var(--accent-red)',
  FATAL: 'var(--accent-red)',
  DEBUG: 'var(--text-2)',
  CMD: 'var(--accent-cyan)',
};

// ── 本窗口条目（本地视图，清屏只清这里）──

let uid = 0;
const entries = ref([]);
const MAX_ENTRIES = 2000;
const TRIM_STEP = 400;

function pushLocal(e) {
  entries.value.push(e);
  // 批量截断：逐条 shift 会每次扰动 DOM 与选区，这是"控制台一直刷新"的帮凶之一
  if (entries.value.length > MAX_ENTRIES) entries.value.splice(0, TRIM_STEP);
}

// ── 吸底滚动：仅当本就贴底且没有进行中的文本选择时才跟随 ──

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

// ── 订阅全局日志流：只收本会话条目 ──

const stream = useLogStream();
function onStreamEntry(e) {
  if (e.session !== mySession) return;
  pushLocal(e);
  maybeScroll();
}
stream.replay(onStreamEntry);   // 补齐窗口打开前的本会话日志
onUnmounted(stream.onEntry(onStreamEntry));

// ── 补全（旧版形态：整条输入是单个 token 时弹层）──

const input = ref('');
const inputEl = ref(null);
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

// 历史翻找填充的输入不弹补全弹层（historyNav 消费一次）：填的是已知完整命令，
// 弹层反而把后续 ↑↓ 劫持成候选切换，历史导航就此失灵（新版终端同款修法）
let historyNav = false;
watch(matches, (m) => {
  if (historyNav) { historyNav = false; suggestOpen.value = false; return; }
  suggestOpen.value = m.length > 0 && input.value.length > 0;
  if (suggestOpen.value) selIdx.value = 0;
});

/** 历史翻找写入输入框；值没变（已到翻找边界）不动标记，避免吞掉下一次键入的弹层。 */
function fillFromHistory(v) {
  if (v === input.value) return;
  historyNav = true;
  input.value = v;
}

function applyMatch(idx) {
  const c = matches.value[idx];
  if (!c) return;
  input.value = '/' + c.name + ' ';
  suggestOpen.value = false;
  inputEl.value && inputEl.value.focus();
}
function onHover(i) {
  selIdx.value = i;
}

async function appendResult(r) {
  if (!r || r.unauthorized) return;
  if (r.ok === false) {
    pushLocal({ id: ++uid, time: '', level: 'ERROR', tag: '', msg: '✕ ' + (r.error || '执行失败') });
  } else if (r.data != null) {
    pushLocal({ id: ++uid, time: '', json: JSON.stringify(r.data, null, 2) });
  }
  maybeScroll();
}

async function submit() {
  const v = input.value;
  if (!v.trim()) return;
  input.value = '';
  suggestOpen.value = false;
  pushLocal({ id: ++uid, time: '', level: 'CMD', tag: '', msg: '> ' + v });
  maybeScroll();
  appendResult(await runCommand(v, mySession));
}

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
  if (e.key === 'ArrowUp' && !suggestOpen.value) { e.preventDefault(); fillFromHistory(historyUp(input.value)); return; }
  if (e.key === 'ArrowDown' && !suggestOpen.value) { e.preventDefault(); fillFromHistory(historyDown()); }
}
</script>

<template>
  <div class="console-app">
    <div ref="logEl" class="log" @scroll="onScroll">
      <div v-for="e in entries" :key="e.id" class="entry">
        <template v-if="e.json != null">
          <JsonBlock :text="e.json" />
        </template>
        <template v-else>
          <span v-if="e.time" class="ts">{{ e.time }}</span>
          <span :style="{ color: e.color || LEVEL_COLOR[e.level] || 'var(--text-0)' }">{{ e.msg }}</span>
        </template>
      </div>
    </div>

    <div class="bar-wrap">
      <div v-if="suggestOpen" class="suggest-wrap">
        <SuggestPopup :matches="matches" :sel-idx="selIdx" show-slash @apply="applyMatch" @hover="onHover" />
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
.suggest-wrap { position: absolute; bottom: 100%; left: 0; right: 0; }

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
