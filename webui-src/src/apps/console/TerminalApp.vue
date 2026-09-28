<script setup>
import { ref, computed, nextTick, watch, inject } from 'vue';
import { useConsole } from './useConsole.js';
import { useShellUser } from '../../composables/useShellUser.js';
import JsonBlock from './JsonBlock.vue';

/**
 * 新版「控制台」——Kali Linux 终端风格。命令不带 / 前缀直发（后端本就剥前缀），
 * 另有一组本地内置命令：whoami / hostname / user / clear / neofetch / exit / echo。
 * 日志流与旧版控制台共享同一单例（useConsole），两套界面看到同一份输出。
 */
const { entries, commands, send, print, clearEntries, historyUp, historyDown } = useConsole();
const shell = useShellUser();
const winApi = inject('winApi', null);

const input = ref('');
const inputEl = ref(null);
const logEl = ref(null);
const selIdx = ref(-1);
const suggestOpen = ref(false);

const PROMPT_BR = '┌──(';
// Kali zsh 约定：root 用户分隔符是 💀、结尾提示符是 #；普通用户是 ㉿ 和 $
const isRoot = computed(() => shell.state.user.toLowerCase() === 'root');
const sep = computed(() => (isRoot.value ? '💀' : '㉿'));
const bar = computed(() => (isRoot.value ? '└─# ' : '└─$ '));

const uptimeStart = Date.now();

function getPartial() {
  // 取输入的首个 token 作为补全候选依据（带参数时不再弹补全，但 Tab 循环态仍需匹配）
  const m = input.value.match(/^\/?(\S*)/);
  return m ? m[1].toLowerCase() : null;
}

const matches = computed(() => {
  const p = getPartial();
  if (p === null) return [];
  return commands.value.filter(c => {
    if (!p) return true;
    if ((c.name || '').toLowerCase().startsWith(p)) return true;
    return (c.aliases || []).some(a => String(a).toLowerCase().startsWith(p));
  });
});

// Linux 式 Tab 补全：首按补全到第一个候选，连按循环切换；
// tabApplied 记录上次补全结果——用户改了输入即退出循环态
let tabApplied = null;
watch(input, (v) => {
  if (tabApplied !== null && v !== tabApplied)
    tabApplied = null;
});

// 补全弹层可见性：单 token 输入时跟随候选；Tab 循环态保持展开
watch([input, matches], () => {
  const cycling = tabApplied !== null && input.value === tabApplied;
  const singleToken = input.value.length > 0 && !/\s/.test(input.value);
  suggestOpen.value = matches.value.length > 0 && (singleToken || cycling);
  if (suggestOpen.value && !cycling)
    selIdx.value = 0;
});

function applyMatch(idx) {
  const c = matches.value[idx];
  if (!c) return;
  input.value = c.name + ' ';
  tabApplied = null;
  suggestOpen.value = false;
  inputEl.value && inputEl.value.focus();
}

async function scrollToEnd() {
  await nextTick();
  if (logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight;
}
watch(entries, scrollToEnd, { deep: false });

// ---- 内置命令（WebUI 本地执行，不走 /api/run）----

const BUILTINS = new Set(['whoami', 'hostname', 'user', 'clear', 'neofetch', 'exit', 'echo']);

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
      clearEntries();
      return true;
    case 'exit':
      winApi ? winApi.close() : print('exit: 无法关闭窗口（缺少窗口环境）', 'var(--accent-red)');
      return true;
    case 'echo':
      print(args.join(' '));
      return true;
    case 'neofetch': {
      const art = [
        ' ██████╗ ████████╗',
        ' ██╔══██╗╚══██╔══╝',
        ' ██║  ██║   ██║   ',
        ' ██║  ██║   ██║   ',
        ' ██████╔╝   ██║   ',
        ' ╚═════╝    ╚═╝   ',
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

async function submit() {
  const v = input.value;
  if (!v.trim()) return;
  input.value = '';
  suggestOpen.value = false;
  // 回显本次输入（Kali 终端每次都打两行提示符；纯文本回显用同款分隔符，不带上色）
  print(`${PROMPT_BR}${shell.state.user}${sep.value}${shell.state.hostname})-[~]`, 'var(--accent-cyan)');
  print(bar.value + v, 'var(--accent-cyan)');
  const t = v.trim().replace(/^\//, '');
  if (BUILTINS.has(t.split(/\s+/)[0].toLowerCase())) {
    runBuiltin(t);
  } else {
    await send(t);
  }
  await scrollToEnd();
}

function onKeydown(e) {
  if (suggestOpen.value && matches.value.length) {
    if (e.key === 'ArrowDown') { e.preventDefault(); selIdx.value = (selIdx.value + 1) % matches.value.length; return; }
    if (e.key === 'ArrowUp') { e.preventDefault(); selIdx.value = (selIdx.value - 1 + matches.value.length) % matches.value.length; return; }
    if (e.key === 'Tab') {
      e.preventDefault();
      // 首按补全第一个候选；输入未被改动时连按循环切换其余候选
      const idx = (tabApplied !== null && input.value === tabApplied)
        ? (selIdx.value + 1) % matches.value.length
        : 0;
      selIdx.value = idx;
      input.value = matches.value[idx].name + ' ';
      tabApplied = input.value;
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
    if (e.key === 'Escape') { e.preventDefault(); suggestOpen.value = false; tabApplied = null; return; }
  }
  if (e.key === 'Enter') { e.preventDefault(); submit(); return; }
  if (e.key === 'ArrowUp' && !suggestOpen.value) { e.preventDefault(); input.value = historyUp(input.value); return; }
  if (e.key === 'ArrowDown' && !suggestOpen.value) { e.preventDefault(); input.value = historyDown(); }
}

function focusInput() {
  inputEl.value && inputEl.value.focus();
}
</script>

<template>
  <div class="terminal-app" @click="focusInput">
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

      <!-- 活动提示符：始终显示在输入行上方 -->
      <div class="prompt p1">
        <span class="p-brace">{{ PROMPT_BR }}</span><span class="p-identity">{{ shell.state.user }}</span><span class="p-identity">{{ sep }}</span><span class="p-identity">{{ shell.state.hostname }}</span><span class="p-brace">)-[</span><span class="p-path">~</span><span class="p-brace">]</span>
      </div>
      <div class="prompt p2">
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

      <div v-if="suggestOpen" class="suggest">
        <div
          v-for="(c, i) in matches"
          :key="c.name"
          class="sug-item"
          :class="{ sel: i === selIdx }"
          @mousedown.prevent="applyMatch(i)"
          @mouseenter="selIdx = i"
        >
          <span class="sug-name">{{ c.name }}</span>
          <span class="sug-desc">{{ c.description || c.usage || '' }}</span>
          <span v-if="c.author" class="sug-author">功能制作者：{{ c.author }}</span>
        </div>
      </div>
    </div>
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

.suggest {
  position: absolute;
  bottom: 100%;
  left: -2px;
  min-width: 320px;
  max-width: 90%;
  max-height: 260px;
  overflow-y: auto;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-sm);
  box-shadow: var(--win-shadow);
}
.sug-item { padding: 7px 12px; cursor: pointer; border-bottom: 1px solid var(--line); }
.sug-item:last-child { border-bottom: none; }
.sug-item.sel, .sug-item:hover { background: var(--surface-2); }
.sug-name { font-family: var(--font-mono); color: var(--accent-cyan); font-size: 12.5px; margin-right: 8px; }
.sug-desc { font-size: 11.5px; color: var(--text-1); }
.sug-author { display: block; font-size: 10.5px; color: var(--text-2); margin-top: 2px; }
</style>
