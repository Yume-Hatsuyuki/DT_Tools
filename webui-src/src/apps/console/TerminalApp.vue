<script setup>
import { ref, computed, watch, nextTick, onMounted, onUnmounted, inject } from 'vue';
import { useCommandInput, newSessionId } from './useCommandInput.js';
import { useLogStream } from '../../composables/useLogStream.js';
import { useShellUser } from '../../composables/useShellUser.js';
import SuggestPopup from '../common/SuggestPopup.vue';
import JsonBlock from './JsonBlock.vue';

/**
 * 新版「控制台」——Kali Linux 终端风格。命令不带 / 前缀直发（后端本就剥前缀），
 * 另有一组本地内置命令：whoami / hostname / user / clear / neofetch / exit / echo。
 * 会话隔离：本窗口只显示自己执行的命令产生的日志（后端按 X-DT-Session 标记回流），
 * 不再打印全部日志——全量日志归「日志」应用，两套控制台互不干扰。
 *
 * 输入行内联在滚动区末尾，与输出一同从上到下流动（真终端行为，不再独立占底部区域）；
 * 提交后按同款配色回显进历史。补全弹层 Teleport 到 body、固定定位在输入行正下方：
 * 窗口下方放不下时由临时撑高（.suggest-spacer）提供滚动余量并把输入行上推，
 * 弹层始终在输入下方展开，绝不遮挡上方输出。
 */
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

// ── 本窗口条目（本地视图，clear 只清这里）──

let uid = 0;
const entries = ref([]);
const MAX_ENTRIES = 2000;
const TRIM_STEP = 400;

function pushLocal(e) {
  entries.value.push(e);
  if (entries.value.length > MAX_ENTRIES) entries.value.splice(0, TRIM_STEP);
}

/** 本地输出（内置命令回显用，不进后端日志）。支持多行文本。 */
function print(text, color) {
  for (const line of String(text).split('\n'))
    pushLocal({ id: ++uid, time: '', level: color ? undefined : 'CMD', tag: '', msg: line, color });
}

// ── 吸底滚动：仅当本就贴底且没有进行中的文本选择时才跟随 ──

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

// ── 订阅全局日志流：只收本会话条目 ──

const stream = useLogStream();
function onStreamEntry(e) {
  if (e.session !== mySession) return;
  pushLocal(e);
  maybeScroll();
}
stream.replay(onStreamEntry);   // 补齐窗口打开前的本会话日志
onUnmounted(stream.onEntry(onStreamEntry));

// ── 命令执行 ──

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
// Kali zsh 约定：root 用户分隔符是 💀、结尾提示符是 #；普通用户是 ㉿ 和 $
const isRoot = computed(() => shell.state.user.toLowerCase() === 'root');
const sep = computed(() => (isRoot.value ? '💀' : '㉿'));
const bar = computed(() => (isRoot.value ? '└─# ' : '└─$ '));

const uptimeStart = Date.now();

// ---- 内置命令（WebUI 本地执行，不走 /api/run）----

const BUILTINS = new Set(['whoami', 'hostname', 'user', 'clear', 'neofetch', 'exit', 'echo']);

/**
 * 内置命令元数据：驱动两处展示——① 补全候选（排在服务端命令之后，白色名 +
 * 「内置」徽标，见 SuggestPopup）；② help 输出末尾的内置命令清单。
 * 服务端 /api/commands 不认识这些本地命令，不在这里声明就没人知道它们存在。
 * 注意：必须声明在 matches 计算属性之前——watch([input, matches]) 会在
 * setup 期间求值 matches，声明在后会触发 TDZ（Cannot access before initialization）。
 */
const BUILTINS_META = [
  { name: 'whoami', usage: 'whoami', description: '显示当前用户名。', builtin: true },
  { name: 'hostname', usage: 'hostname [新主机名]', description: '查看或修改主机名（仅本浏览器）。', builtin: true },
  { name: 'user', usage: 'user <新用户名>', description: '修改用户名（终端提示符同步更新）。', builtin: true },
  { name: 'echo', usage: 'echo <文本>', description: '原样输出文本。', builtin: true },
  { name: 'clear', usage: 'clear', description: '清空本窗口的输出（不动服务端日志）。', builtin: true },
  { name: 'neofetch', usage: 'neofetch', description: '显示终端与桌面环境信息。', builtin: true },
  { name: 'exit', usage: 'exit', description: '关闭当前终端窗口。', builtin: true },
];

// ── 补全（Linux 式 Tab 循环）──

const input = ref('');
const inputEl = ref(null);
const inputLineEl = ref(null);
const selIdx = ref(-1);
const suggestOpen = ref(false);

function getPartial() {
  // 取输入的首个 token 作为补全候选依据（带参数时不再弹补全，但 Tab 循环态仍需匹配）
  const m = input.value.match(/^\/?(\S*)/);
  return m ? m[1].toLowerCase() : null;
}

const matches = computed(() => {
  const p = getPartial();
  if (p === null) return [];
  // 服务端命令在前；内置命令排最后（"底层"）：白色名 + 内置徽标，一眼可辨
  const server = commands.value.filter(c => {
    if (!p) return true;
    if ((c.name || '').toLowerCase().startsWith(p)) return true;
    return (c.aliases || []).some(a => String(a).toLowerCase().startsWith(p));
  });
  const local = BUILTINS_META.filter(b => !p || b.name.startsWith(p));
  return [...server, ...local];
});

// Linux 式 Tab 补全：TAB 采纳当前高亮候选（↑↓/悬停走到哪就补全哪个，与旧版
// 控制台一致），高亮停在上次补全位时连按 TAB 循环切下一个；tabApplied 记录
// 上次补全写入的输入——用户改了输入即退出循环态。
// 循环态必须冻结候选快照（cycleList）：补全写入完整命令名后实时 matches 会
// 收窄成单条，循环若跟实时列表就永远卡在原地；弹层与 ↑↓/TAB 循环都读快照。
let tabApplied = null;
let tabAppliedIdx = -1;
const cycleList = ref(null);

function isCycling() {
  return cycleList.value !== null && tabApplied !== null && input.value === tabApplied;
}
/** 弹层与键盘导航共用的候选列表：循环态=冻结快照，其余=实时匹配。 */
const viewMatches = computed(() => (isCycling() ? cycleList.value : matches.value));

function exitCycle() {
  cycleList.value = null;
  tabApplied = null;
  tabAppliedIdx = -1;
}

watch(input, (v) => {
  if (tabApplied !== null && v !== tabApplied) exitCycle();
});

// 补全弹层可见性：单 token 输入时跟随候选；Tab 循环态保持展开。
// 历史翻找填充的输入不弹弹层（historyNav 消费一次）：填的是已知完整命令，
// 弹层反而把后续 ↑↓ 劫持成候选切换，历史导航就此失灵（旧版控制台同款修法）
let historyNav = false;
watch([input, matches], () => {
  if (historyNav) { historyNav = false; suggestOpen.value = false; return; }
  const cycling = isCycling();
  const singleToken = input.value.length > 0 && !/\s/.test(input.value);
  suggestOpen.value = matches.value.length > 0 && (singleToken || cycling);
  if (suggestOpen.value && !cycling)
    selIdx.value = 0;
});

/** 历史翻找写入输入框；值没变（已到翻找边界）不动标记，避免吞掉下一次键入的弹层。 */
function fillFromHistory(v) {
  if (v === input.value) return;
  historyNav = true;
  input.value = v;
}

// ── 补全弹层定位：输入行正下方（Teleport 到 body 的固定定位）──

const popupEl = ref(null);
const sugPos = ref({ left: 0, top: 0 });
const spacerH = ref(0);
const SUG_GAP = 4;

/** 锚定输入行：先按弹层高度撑出滚动余量，下方放不下就把输入行滚上来。 */
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
  const c = viewMatches.value[idx];   // 弹层展示与点击采纳同一份列表（循环态=快照）
  if (!c) return;
  exitCycle();
  input.value = c.name + ' ';
  suggestOpen.value = false;
  inputEl.value && inputEl.value.focus();
}
function onHover(i) {
  selIdx.value = i;
}

// ---- 内置命令的执行分发（WebUI 本地执行，不走 /api/run）----

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
      entries.value.splice(0, entries.value.length);   // 只清本地视图，不动服务端环形缓冲
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

/** 命令回显：与活动提示符同款配色的两行提示符进历史（不再丢色）。 */
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

/** help 输出末尾追加的内置命令清单（跟随 /help 一起列出，排在服务端命令之后）。 */
function printBuiltins() {
  print('━━━ 终端内置命令（本地自带，不经服务端）━━━', 'var(--text-2)');
  for (const b of BUILTINS_META)
    print(b.usage.padEnd(22) + b.description, 'var(--text-0)');
  print('提示: help <内置命令名> 查看单条说明', 'var(--text-2)');
}

/** help <内置命令名> 的单条说明（服务端不认识内置命令，这里直接本地作答）。 */
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
      // help <内置命令名>：服务端会答"未知命令"，本地直接作答
      printBuiltinDetail(local);
    } else {
      await appendResult(await runCommand(t, mySession));
      if (!arg) {
        // 服务端命令清单经日志流回流（轮询增量 ≤0.8s），稍候再追加内置清单，
        // 保证内置段排在服务端清单之后（"底层"位置）
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
      // 采纳当前高亮（↑↓/悬停选定后再按 TAB 即补全那条）；高亮没挪动
      // （高亮==上次补全位）时连按 TAB = 循环切到快照里的下一个候选
      const cycling = isCycling();
      const idx = cycling && selIdx.value === tabAppliedIdx
        ? (selIdx.value + 1) % list.length
        : Math.max(0, Math.min(selIdx.value, list.length - 1));
      const applied = list[idx];
      if (!applied) return;
      if (!cycling) cycleList.value = matches.value.slice();   // 进入循环态：冻结本轮候选
      input.value = applied.name + ' ';
      tabApplied = input.value;
      // 高亮对齐到刚补全的那条（快照列表里它可能在任意位置，别名命令尤其如此）
      const ni = list.findIndex(m => m.name === applied.name);
      selIdx.value = ni >= 0 ? ni : 0;
      tabAppliedIdx = selIdx.value;
      return;
    }
    if (e.key === 'Enter' && !input.value.includes(' ')) {
      // 未带参数时 Enter 采纳当前候选；已补全（循环态）或带参数则直接执行
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

/**
 * 点击行为：光标落在日志文本且已选中内容时不抢焦点——聚焦输入框会清空
 * 文本选区（旧版"无法复制"的根因）；选中文本期间随便点都不丢选区。
 */
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
  background: #0f1424;   /* 与窗口 chrome 同源的深蓝黑 */
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
/* Kali：提示符身份段（user💀host / user㉿host）整体为 kali-red */
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

/* 弹层悬浮于输入行下方（body 层固定定位，坐标由 placeSuggest 计算） */
.suggest-float {
  position: fixed;
  z-index: 9999;
  min-width: 320px;
  max-width: 90vw;
}
/* 弹层在输入下方展开：恢复四边描边与完整圆角（组件默认按"悬于输入上方"造型） */
.suggest-float :deep(.suggest) {
  border-bottom: 1px solid var(--win-border-active);
  border-radius: var(--radius-sm);
  box-shadow: var(--win-shadow);
}
</style>
