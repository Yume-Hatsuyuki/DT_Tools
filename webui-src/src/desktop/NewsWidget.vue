<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { API, createPoller } from '../api.js';
import { t, locale, localeInfo, intlLocale } from '../i18n/index.js';
import IconX from '~icons/tabler/x';
import IconRefresh from '~icons/tabler/refresh';

/**
 * 游戏新闻挂件（桌面挂件，不是应用）：读取 Steam 新闻 RSS，经后端 /api/steam/news 中转。
 * 语言跟随全局界面语言（i18n），映射到 Steam 的 l= 参数；不再单独选择语言。
 * 可拖动、可关闭（经底部 Dock 再开）。位置记在 localStorage（仅本浏览器）。
 */
const emit = defineEmits(['close']);

const STORE_KEY = 'dt_news_widget_v1';
// 与 Window.vue 的 TOPBAR_H / tokens.css --topbar-h 同步：挂件不拖进顶栏之下
const TOPBAR_H = 32;
const POLL_MS = 60000;   // 每 1 分钟拉取一次（与在线人数同频）；后端缓存短于此间隔

function readStore() {
  try { return JSON.parse(localStorage.getItem(STORE_KEY)) || {}; } catch { return {}; }
}

const saved = readStore();
const pos = ref(saved.pos || null);   // {left, top}；null=默认停靠在 Dock 之上的右下角
const items = ref([]);
const status = ref('loading');        // loading | ok | error
const fallback = ref(false);
const rootEl = ref(null);

const dateFmt = computed(() => new Intl.DateTimeFormat(intlLocale(), { year: 'numeric', month: 'short', day: 'numeric' }));

function fmtDate(iso) {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : dateFmt.value.format(d);
}

function persist(closed) {
  try {
    localStorage.setItem(STORE_KEY, JSON.stringify({ closed, pos: pos.value }));
  } catch { /* 隐私模式忽略 */ }
}

// 序号防竞态：切换语言后，旧语言的响应到达时直接丢弃
let seq = 0;
async function fetchNews() {
  const requested = localeInfo.value.steam;
  const mine = ++seq;
  if (!items.value.length) status.value = 'loading';
  const d = await API.steamNews(requested);
  if (mine !== seq) return;
  if (!d.ok || !Array.isArray(d.items)) {
    status.value = 'error';
    return;
  }
  items.value = d.items;
  fallback.value = !!d.fallback;
  status.value = 'ok';
}

const poller = createPoller(fetchNews, POLL_MS);

// 全局语言变化：清空旧列表并按新语言重新拉取
watch(locale, () => {
  items.value = [];
  fallback.value = false;
  fetchNews();
});

onMounted(() => {
  persist(false);
  poller.start();
});

onUnmounted(() => {
  poller.stop();
  window.removeEventListener('pointermove', onMove);
  window.removeEventListener('pointerup', onUp);
});

function close() {
  persist(true);
  emit('close');
}

// ---- 标题栏拖动（视口内钳位，与 SteamWidget 同款） ----

let drag = null;

function onHeadDown(e) {
  if (e.target.closest('button')) return;
  const r = rootEl.value.getBoundingClientRect();
  drag = { dx: e.clientX - r.left, dy: e.clientY - r.top };
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}

function onMove(e) {
  const el = rootEl.value;
  const left = Math.min(Math.max(8, e.clientX - drag.dx), window.innerWidth - el.offsetWidth - 8);
  const top = Math.min(Math.max(TOPBAR_H + 8, e.clientY - drag.dy), window.innerHeight - el.offsetHeight - 8);
  pos.value = { left, top };
}

function onUp() {
  drag = null;
  window.removeEventListener('pointermove', onMove);
  window.removeEventListener('pointerup', onUp);
  persist(false);
}

const posStyle = computed(() => (pos.value
  ? { left: pos.value.left + 'px', top: pos.value.top + 'px', right: 'auto', bottom: 'auto' }
  : {}));
</script>

<template>
  <div ref="rootEl" class="news-widget" :style="posStyle">
    <div class="nw-head" @pointerdown="onHeadDown">
      <span class="nw-title">《Deadly Trick》{{ t('news.short') }}</span>
      <button class="nw-icon" :title="t('common.refresh')" @click="fetchNews"><IconRefresh /></button>
      <button class="nw-icon nw-close" :title="t('common.closeHint')" @click="close"><IconX /></button>
    </div>

    <div class="nw-body">
      <div v-if="status === 'loading' && !items.length" class="nw-state">{{ t('common.loading') }}</div>
      <div v-else-if="status === 'error' && !items.length" class="nw-state err">{{ t('news.error') }}</div>
      <div v-else-if="!items.length" class="nw-state">{{ t('news.empty') }}</div>
      <ul v-else class="nw-list">
        <li v-for="it in items" :key="it.url" class="nw-item">
          <img v-if="it.image" class="nw-thumb" :src="it.image" alt="" loading="lazy" draggable="false" />
          <div class="nw-text">
            <a class="nw-link" :href="it.url" target="_blank" rel="noopener noreferrer" :title="it.title">{{ it.title }}</a>
            <div class="nw-meta">{{ fmtDate(it.date) }}</div>
            <p v-if="it.summary" class="nw-sum">{{ it.summary }}</p>
          </div>
        </li>
      </ul>
    </div>

    <div class="nw-foot">
      <span v-if="fallback" class="nw-note">{{ t('news.fallback') }}</span>
      <span class="nw-src">{{ t('news.source') }}</span>
    </div>
  </div>
</template>

<style scoped>
.news-widget {
  position: fixed;
  z-index: 4500;   /* 与 SteamWidget 同层：悬于窗口与粒子之上，低于顶栏/Dock */
  width: 320px;
  right: 14px;
  bottom: calc(var(--dock-h) + 14px);
  max-height: min(460px, calc(100vh - var(--topbar-h) - var(--dock-h) - 28px));
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: 14px;
  background: rgba(9, 18, 26, 0.82);
  backdrop-filter: blur(20px) saturate(150%);
  border: 1px solid var(--dock-border);
  box-shadow: 0 14px 40px -12px rgba(2, 10, 18, 0.7);
  user-select: none;
}
.nw-head {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 8px 6px 12px;
  cursor: grab;
}
.news-widget:active .nw-head { cursor: grabbing; }
.nw-title {
  flex: 1;
  min-width: 0;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--text-0);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.nw-lang {
  max-width: 116px;
  font-size: 11px;
  color: var(--text-1);
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  padding: 2px 4px;
  cursor: pointer;
}
.nw-icon {
  width: 22px;
  height: 22px;
  display: grid;
  place-items: center;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
}
.nw-icon:hover { background: rgba(140, 180, 255, 0.12); color: var(--text-0); }
.nw-icon svg { width: 12px; height: 12px; }
.nw-close:hover { background: rgba(255, 82, 82, 0.15); color: var(--accent-red); }
.nw-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  border-top: 1px solid var(--line);
}
.nw-state { padding: 22px 12px; text-align: center; font-size: 12px; color: var(--text-2); }
.nw-state.err { color: var(--accent-red); }
.nw-list { list-style: none; margin: 0; padding: 0; }
.nw-item {
  display: flex;
  gap: 10px;
  padding: 9px 12px;
  border-bottom: 1px solid var(--line);
}
.nw-item:last-child { border-bottom: none; }
.nw-thumb {
  width: 44px;
  height: 44px;
  flex: none;
  border-radius: 6px;
  object-fit: cover;
  background: rgba(255, 255, 255, 0.05);
}
.nw-text { min-width: 0; flex: 1; }
.nw-link {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  font-size: 12.5px;
  font-weight: 500;
  line-height: 1.35;
  color: var(--text-0);
  text-decoration: none;
}
.nw-link:hover { color: var(--accent-cyan); text-decoration: underline; }
.nw-meta { font-family: var(--font-mono); font-size: 10px; color: var(--text-2); margin-top: 2px; }
.nw-sum {
  margin: 3px 0 0;
  font-size: 11px;
  line-height: 1.45;
  color: var(--text-1);
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.nw-foot {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 12px;
  border-top: 1px solid var(--line);
  font-size: 10px;
  color: var(--text-2);
}
.nw-note { color: var(--accent-amber); }
.nw-src { margin-left: auto; white-space: nowrap; }
</style>
