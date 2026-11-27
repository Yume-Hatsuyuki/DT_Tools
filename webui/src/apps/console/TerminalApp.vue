<script setup>
import { ref, computed, watch, nextTick, onMounted, onUnmounted, inject } from 'vue';
import { useCommandInput, newSessionId } from './useCommandInput.js';
import { useLogStream } from '../../composables/useLogStream.js';
import { useShellUser } from '../../composables/useShellUser.js';
import SuggestPopup from '../common/SuggestPopup.vue';
import JsonBlock from './JsonBlock.vue';

const { commands, historyUp, historyDown, runCommand } = useCommandInput('terminal');
const mySession = newSessionId();
const shell = useShellUser();
const winApi = inject('winApi', null);
const LEVEL_COLOR = {
  WARN: 'var(--accent-amber)',
  ERROR: 'var(--accent-red)',
  FATAL: 'var(--accent-red)',
  DEBUG: 'var(--text-2)',
  CMD: 'var(--accent-cyan)',
};

let uid = 0;
const entries = ref([]);
const MAX_ENTRIES = 2000;
const TRIM_STEP = 400;

function pushLocal(e) {
  entries.value.push(e);
  if (entries.value.length > MAX_ENTRIES) entries.value.splice(0, TRIM_STEP);
}

function print(text, color) {
  for (const line of String(text).split('\n'))
    pushLocal({ id: ++uid, time: '', level: color ? undefined : 'CMD', tag: '', msg: line, color });
}

const logEl = ref(null);
const stick = ref(true);

function onScroll() {
  const el = logEl.value;
  stick.value = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
  if (suggestOpen.value) placeSuggest();
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

const stream = useLogStream();
function onStreamEntry(e) {
  if (e.session !== mySession) return;
  pushLocal(e);
  maybeScroll();
}
stream.replay(onStreamEntry);
onUnmounted(stream.onEntry(onStreamEntry));

async function appendResult(r) {
  if (!r || r.unauthorized) return;
  if (r.ok === false) {
    pushLocal({ id: ++uid, time: '', level: 'ERROR', tag: '', msg: '✕ ' + (r.error || '执行失败') });
  } else if (r.data != null) {
    pushLocal({ id: ++uid, time: '', json: JSON.stringify(r.data, null, 2) });
  }
  maybeScroll();
}

const PROMPT_BR = '┌──(';
const isRoot = computed(() => shell.state.user.toLowerCase() === 'root');
const sep = computed(() => (isRoot.value ? '💀' : '㉿'));
const bar = computed(() => (isRoot.value ? '└─# ' : '└─$ '));

const uptimeStart = Date.now();

const BUILTINS = new Set(['whoami', 'hostname', 'user', 'clear', 'neofetch', 'exit', 'echo']);

const BUILTINS_META = [
  { name: 'whoami', usage: 'whoami', description: '显示当前用户名。', builtin: true },
  { name: 'hostname', usage: 'hostname [新主机名]', description: '查看或修改主机名（仅本浏览器）。', builtin: true },
  { name: 'user', usage: 'user <新用户名>', description: '修改用户名（终端提示符同步更新）。', builtin: true },
  { name: 'echo', usage: 'echo <文本>', description: '原样输出文本。', builtin: true },
  { name: 'clear', usage: 'clear', description: '清空本窗口的输出（不动服务端日志）。', builtin: true },
  { name: 'neofetch', usage: 'neofetch', description: '显示终端与桌面环境信息。', builtin: true },
  { name: 'exit', usage: 'exit', description: '关闭当前终端窗口。', builtin: true },
];

const input = ref('');
const inputEl = ref(null);
const inputLineEl = ref(null);
const selIdx = ref(-1);
const suggestOpen = ref(false);

function getPartial() {
  const m = input.value.match(/^\/?(\S*)/);
  return m ? m[1].toLowerCase() : null;
}

const matches = computed(() => {
  const p = getPartial();
  if (p === null) return [];
  const server = commands.value.filter(c => {
    if (!p) return true;
    if ((c.name || '').toLowerCase().startsWith(p)) return true;
    return (c.aliases || []).some(a => String(a).toLowerCase().startsWith(p));
  });
  const local = BUILTINS_META.filter(b => !p || b.name.startsWith(p));
  return [...server, ...local];
});

let tabApplied = null;
let tabAppliedIdx = -1;
const cycleList = ref(null);

function isCycling() {
  return cycleList.value !== null && tabApplied !== null && input.value === tabApplied;
}
const viewMatches = computed(() => (isCycling() ? cycleList.value : matches.value));

function exitCycle() {
  cycleList.value = null;
  tabApplied = null;
  tabAppliedIdx = -1;
}

watch(input, (v) => {
  if (tabApplied !== null && v !== tabApplied) exitCycle();
});

let historyNav = false;
watch([input, matches], () => {
  if (historyNav) { historyNav = false; suggestOpen.value = false; return; }
  const cycling = isCycling();
  const singleToken = input.value.length > 0 && !/\s/.test(input.value);
  suggestOpen.value = matches.value.length > 0 && (singleToken || cycling);
  if (suggestOpen.value && !cycling)
    selIdx.value = 0;
});

function fillFromHistory(v) {
  if (v === input.value) return;
  historyNav = true;
  input.value = v;
}

const popupEl = ref(null);
const sugPos = ref({ left: 0, top: 0 });
const spacerH = ref(0);
const SUG_GAP = 4;

async function placeSuggest() {
  if (!suggestOpen.value) return;
  await nextTick();
  const popup = popupEl.value;
  const line = inputLineEl.value;
  const log = logEl.value;
  if (!popup || !line || !log) return;
  spacerH.value = popup.offsetHeight + SUG_GAP;
  await nextTick();
  if (window.innerHeight - line.getBoundingClientRect().bottom < spacerH.value)
    log.scrollTop = log.scrollHeight;
  await nextTick();
  const r = line.getBoundingClientRect();
  sugPos.value = {
    left: Math.max(8, Math.min(r.left, window.innerWidth - popup.offsetWidth - 8)),
    top: Math.max(8, r.bottom + SUG_GAP),
  };
}

watch(suggestOpen, (open) => {
  if (open) placeSuggest();
  else spacerH.value = 0;
});
watch(matches, () => { if (suggestOpen.value) placeSuggest(); });

function onWindowResize() {
  if (suggestOpen.value) placeSuggest();
}
onMounted(() => window.addEventListener('resize', onWindowResize));
onUnmounted(() => window.removeEventListener('resize', onWindowResize));

function applyMatch(idx) {
  const c = viewMatches.value[idx];
  if (!c) return;
  exitCycle();
  input.value = c.name + ' ';
  suggestOpen.value = false;
  inputEl.value && inputEl.value.focus();
}
function onHover(i) {
  selIdx.value = i;
}

function fmtUptime() {
  const s = Math.floor((Date.now() - uptimeStart) / 1000);
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60);
  return `${h}h ${m}m ${s % 60}s`;
}

function runBuiltin(raw) {
  const parts = raw.split(/\s+/);
  const cmd = parts[0].toLowerCase();
  const args = parts.slice(1);

  switch (cmd) {
    case 'whoami':
      print(shell.state.user);
      return true;
    case 'hostname': {
      if (!args.length) { print(shell.state.hostname); return true; }
      const old = shell.state.hostname;
      const r = shell.setHostname(args.join(' '));
      if (!r.ok) { print('hostname: ' + r.error, 'var(--accent-red)'); return true; }
      print(`主机名：${old} → ${shell.state.hostname}`);
      return true;
    }
    case 'user':
      if (!args.length) { print('用法: user <新用户名>', 'var(--accent-amber)'); return true; }
      {
        const old = shell.state.user;
        const r = shell.setUser(args.join(' '));
        if (!r.ok) { print('user: ' + r.error, 'var(--accent-red)'); return true; }
        print(`${old} → ${shell.state.user}（左下角铭牌同步更新）`);
        return true;
      }
    case 'clear':
      entries.value.splice(0, entries.value.length);
      return true;
    case 'exit':
      winApi ? winApi.close() : print('exit: 无法关闭窗口（缺少窗口环境）', 'var(--accent-red)');
      return true;
    case 'echo':
      print(args.join(' '));
      return true;
    case 'neofetch': {
      const art = [
        ' ██████╗ ████████╗         ████████╗ ██████╗  ██████╗ ██╗      ███████╗ ',
        ' ██╔══██╗╚══██╔══╝         ╚══██╔══╝██╔═══██╗██╔═══██╗██║      ██╔════╝ ',
        ' ██║  ██║   ██║               ██║   ██║   ██║██║   ██║██║      ███████╗ ',
        ' ██║  ██║   ██║               ██║   ██║   ██║██║   ██║██║      ╚════██║ ',
        ' ██████╔╝   ██║               ██║   ╚██████╔╝╚██████╔╝███████╗ ███████║ ',
        ' ╚═════╝    ╚═╝   ▄▄▄▄▄▄▄▄▄   ╚═╝    ╚═════╝  ╚═════╝ ╚══════╝ ╚══════╝ ',
      ].join('\n');
      const info = [
        `${shell.state.user}@${shell.state.hostname}`,
        '-----------------',
        `OS: Deadly Trick WebUI (Kali 主题)`,
        `Shell: dt-shell 1.0`,
        `Uptime: ${fmtUptime()}`,
        `Windows: ${document.querySelectorAll('.win').length}`,
        `Resolution: ${window.innerWidth}x${window.innerHeight}`,
      ].join('\n');
      print(art, 'var(--accent-cyan)');
      print(info, 'var(--accent-green)');
      return true;
    }
  }
  return false;
}

function printPrompt(cmd) {
  pushLocal({
    id: ++uid,
    prompt: {
      user: shell.state.user,
      sep: sep.value,
      host: shell.state.hostname,
      bar: bar.value,
      cmd,
    },
  });
}

function printBuiltins() {
  print('━━━ 终端内置命令（本地自带，不经服务端）━━━', 'var(--text-2)');
  for (const b of BUILTINS_META)
    print(b.usage.padEnd(22) + b.description, 'var(--text-0)');
  print('提示: help <内置命令名> 查看单条说明', 'var(--text-2)');
}

function printBuiltinDetail(b) {
  print(b.usage, 'var(--text-0)');
  print('      ' + b.description, 'var(--text-2)');
  print('      终端内置命令：本地执行，不经服务端。', 'var(--text-2)');
}

async function submit() {
  const v = input.value;
  if (!v.trim()) return;
  input.value = '';
  suggestOpen.value = false;
  printPrompt(v);
  const t = v.trim().replace(/^\//, '');
  const parts = t.split(/\s+/);
  const head = parts[0].toLowerCase();

  if (BUILTINS.has(head)) {
    runBuiltin(t);
  } else if (head === 'help') {
    const arg = (parts[1] || '').toLowerCase().replace(/^\//, '');
    const local = arg && BUILTINS_META.find(b => b.name === arg);
    if (local) {
      printBuiltinDetail(local);
    } else {
      await appendResult(await runCommand(t, mySession));
      if (!arg) {
        await new Promise(r => setTimeout(r, 1200));
        printBuiltins();
      }
    }
  } else {
    await appendResult(await runCommand(t, mySession));
  }
  maybeScroll();
}

function onKeydown(e) {
  if (suggestOpen.value && matches.value.length) {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const l = viewMatches.value;
      selIdx.value = (selIdx.value + 1) % l.length;
      return;
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault();
      const l = viewMatches.value;
      selIdx.value = (selIdx.value - 1 + l.length) % l.length;
      return;
    }
    if (e.key === 'Tab') {
      e.preventDefault();
      const list = viewMatches.value;
      if (!list.length) return;
      const cycling = isCycling();
      const idx = cycling && selIdx.value === tabAppliedIdx
        ? (selIdx.value + 1) % list.length
        : Math.max(0, Math.min(selIdx.value, list.length - 1));
      const applied = list[idx];
      if (!applied) return;
      if (!cycling) cycleList.value = matches.value.slice();
      input.value = applied.name + ' ';
      tabApplied = input.value;
      const ni = list.findIndex(m => m.name === applied.name);
      selIdx.value = ni >= 0 ? ni : 0;
      tabAppliedIdx = selIdx.value;
      return;
    }
    if (e.key === 'Enter' && !input.value.includes(' ')) {
      e.preventDefault();
      if (tabApplied === null) { applyMatch(selIdx.value); return; }
      suggestOpen.value = false;
      submit();
      return;
    }
    if (e.key === 'Escape') { e.preventDefault(); suggestOpen.value = false; exitCycle(); return; }
  }
  if (e.key === 'Enter') { e.preventDefault(); submit(); return; }
  if (e.key === 'ArrowUp' && !suggestOpen.value) { e.preventDefault(); fillFromHistory(historyUp(input.value)); return; }
  if (e.key === 'ArrowDown' && !suggestOpen.value) { e.preventDefault(); fillFromHistory(historyDown()); }
}

function focusInput() {
  inputEl.value && inputEl.value.focus();
}

function onRootClick() {
  const sel = document.getSelection();
  if (sel && !sel.isCollapsed && logEl.value && logEl.value.contains(sel.anchorNode)) return;
  focusInput();
}
</script>

<template>
  <div class="terminal-app" @click="onRootClick">
    <div ref="logEl" class="log" @scroll="onScroll">
      <div v-for="e in entries" :key="e.id" class="entry">
        <template v-if="e.json != null">
          <JsonBlock :text="e.json" />
        </template>
        <template v-else-if="e.prompt">
          <div class="prompt p1">
            <span class="p-brace">{{ PROMPT_BR }}</span><span class="p-identity">{{ e.prompt.user }}</span><span class="p-identity">{{ e.prompt.sep }}</span><span class="p-identity">{{ e.prompt.host }}</span><span class="p-brace">)-[</span><span class="p-path">~</span><span class="p-brace">]</span>
          </div>
          <div class="prompt p2">
            <span class="p-bar">{{ e.prompt.bar }}</span><span class="cmd-text">{{ e.prompt.cmd }}</span>
          </div>
        </template>
        <template v-else>
          <span v-if="e.time" class="ts">{{ e.time }}</span>
          <span :style="{ color: e.color || LEVEL_COLOR[e.level] || 'var(--text-0)' }">{{ e.msg }}</span>
        </template>
      </div>

      <!-- 活动提示符：内联在滚动区末尾，随内容从上到下流动 -->
      <div class="prompt p1 live">
        <span class="p-brace">{{ PROMPT_BR }}</span><span class="p-identity">{{ shell.state.user }}</span><span class="p-identity">{{ sep }}</span><span class="p-identity">{{ shell.state.hostname }}</span><span class="p-brace">)-[</span><span class="p-path">~</span><span class="p-brace">]</span>
      </div>
      <div ref="inputLineEl" class="prompt p2">
        <span class="p-bar">{{ bar }}</span><input
          ref="inputEl"
          v-model="input"
          class="term-input"
          type="text"
          autocomplete="off"
          autocapitalize="off"
          spellcheck="false"
          @keydown="onKeydown"
          @blur="suggestOpen = false"
        >
      </div>
      <!-- 补全弹层展开期间临时撑高：给"弹层在输入下方"留出滚动余量 -->
      <div class="suggest-spacer" :style="{ height: spacerH + 'px' }"></div>
    </div>

    <!-- 补全弹层：Teleport 到 body（窗口外壳 backdrop-filter 会劫持 fixed 包含块），
         锚在输入行正下方，绝不遮挡上方输出 -->
    <Teleport to="body">
      <div
        v-if="suggestOpen"
        ref="popupEl"
        class="suggest-float"
        :style="{ left: sugPos.left + 'px', top: sugPos.top + 'px' }"
      >
        <SuggestPopup :matches="viewMatches" :sel-idx="selIdx" @apply="applyMatch" @hover="onHover" />
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.terminal-app {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #0f1424;
  cursor: text;
}

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

.prompt { white-space: pre-wrap; user-select: none; }
.p1 { margin-top: 8px; }
.p-brace { color: var(--accent-cyan); font-weight: 600; }
.p-identity { color: var(--kali-red); font-weight: 600; }
.p-path { color: var(--accent-magenta); }
.p-bar { color: var(--accent-cyan); font-weight: 600; }
.cmd-text { color: var(--text-0); }

.term-input {
  flex: 1;
  background: transparent;
  border: none;
  outline: none;
  color: var(--text-0);
  font-family: var(--font-mono);
  font-size: 12.5px;
  caret-color: var(--accent-cyan);
  width: calc(100% - 3em);
}
.p2 { display: flex; align-items: baseline; position: relative; }

.suggest-spacer { pointer-events: none; }

.suggest-float {
  position: fixed;
  z-index: 9999;
  min-width: 320px;
  max-width: 90vw;
}
.suggest-float :deep(.suggest) {
  border-bottom: 1px solid var(--win-border-active);
  border-radius: var(--radius-sm);
  box-shadow: var(--win-shadow);
}
</style>
