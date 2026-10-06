<script setup>
import { computed } from 'vue';
import { useUpdateCheck } from '../composables/useUpdateCheck.js';
import IconDownload from '~icons/tabler/download';
import IconRefresh from '~icons/tabler/refresh';
import IconExternalLink from '~icons/tabler/external-link';
import IconFolderOpen from '~icons/tabler/folder-open';
import IconX from '~icons/tabler/x';

/**
 * 更新信息弹窗：列出新版本信息（版本号、发布时间、更新说明、安装包资产），
 * 并提供下载（后台线程，进度轮询）与"打开所在文件夹"。数据来自全局单例
 * useUpdateCheck（顶栏徽标/气泡与关于弹窗共用），本组件不自持状态。
 * 更新说明是 GitHub Release 的 Markdown 正文：只做极简安全渲染
 * （标题/列表/加粗/链接），全部经模板插值进 DOM（等价 textContent），不用 v-html。
 */
const emit = defineEmits(['close']);

const upd = useUpdateCheck();
const s = upd.state;

const latest = computed(() => s.latest);
const hasUpdate = computed(() => s.hasUpdate && latest.value);
const zipAsset = computed(() => {
  const list = (latest.value && latest.value.assets) || [];
  return list.find(a => (a.name || '').toLowerCase().endsWith('.zip')) || null;
});
const download = computed(() => s.download);
const percent = computed(() => {
  const d = download.value;
  if (!d || !d.total) return 0;
  return Math.min(100, Math.round((d.received / d.total) * 100));
});

function fmtSize(n) {
  if (!n || n <= 0) return '';
  if (n >= 1024 * 1024) return (n / 1024 / 1024).toFixed(1) + ' MB';
  return Math.max(1, Math.round(n / 1024)) + ' KB';
}
function fmtDate(text) {
  if (!text) return '';
  const d = new Date(text);
  return isNaN(d.getTime()) ? text : d.toLocaleString('zh-CN', { hour12: false });
}

// ---- 极简 Markdown 块解析：标题/列表/段落/空行 + 行内 [文本](链接)/**加粗**/裸链接 ----

const INLINE_RE = /\[([^\]]+)\]\((https?:[^)\s]+)\)|\*\*([^*]+)\*\*|(https?:\/\/[^\s)]+)/g;

function inlineTokens(line) {
  const tokens = [];
  let last = 0;
  let m;
  INLINE_RE.lastIndex = 0;
  while ((m = INLINE_RE.exec(line))) {
    if (m.index > last) tokens.push({ t: 'text', s: line.slice(last, m.index) });
    if (m[1] !== undefined) tokens.push({ t: 'link', s: m[1], href: m[2] });
    else if (m[3] !== undefined) tokens.push({ t: 'bold', s: m[3] });
    else tokens.push({ t: 'link', s: m[4], href: m[4] });
    last = INLINE_RE.lastIndex;
  }
  if (last < line.length) tokens.push({ t: 'text', s: line.slice(last) });
  return tokens.length ? tokens : [{ t: 'text', s: line }];
}

const blocks = computed(() => {
  const notes = (latest.value && latest.value.notes) || '';
  const out = [];
  for (const raw of notes.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) { out.push({ t: 'gap', inl: [] }); continue; }
    const h = line.match(/^#{1,6}\s+(.*)/);
    if (h) out.push({ t: 'h', inl: inlineTokens(h[1]) });
    else if (/^[*\-+]\s+/.test(line)) out.push({ t: 'li', inl: inlineTokens(line.replace(/^[*\-+]\s+/, '')) });
    else out.push({ t: 'p', inl: inlineTokens(line) });
  }
  return out;
});
</script>

<template>
  <div class="upd-mask" @click.self="emit('close')" @contextmenu.prevent>
    <div class="upd-box">
      <div class="upd-head">
        <div class="upd-title"><IconDownload class="upd-title-icon" /> 软件更新</div>
        <button class="upd-close" title="关闭" @click="emit('close')"><IconX /></button>
      </div>

      <!-- 版本对照 -->
      <div class="upd-versions">
        <span class="upd-ver">当前版本 <b>v{{ s.current || '…' }}</b></span>
        <template v-if="hasUpdate">
          <span class="upd-arrow">→</span>
          <span class="upd-ver new">最新版本 <b>{{ latest.tag }}</b></span>
        </template>
      </div>
      <div v-if="hasUpdate" class="upd-newline">
        发现新版本！发布于 {{ fmtDate(latest.publishedAt) }}
      </div>
      <div v-else-if="s.checking" class="upd-idle">正在检查更新…</div>
      <div v-else-if="latest" class="upd-idle ok">当前已是最新版本。</div>
      <div v-else-if="s.error" class="upd-idle err">检查失败：{{ s.error }}</div>
      <div v-else class="upd-idle">尚未检测过更新，点下方"重新检查"。</div>

      <!-- 更新说明（GitHub Release 正文，迷你 Markdown） -->
      <div v-if="latest" class="upd-notes">
        <div class="upd-notes-title">更新说明</div>
        <div class="upd-notes-body">
          <div v-for="(b, i) in blocks" :key="i" :class="'nb-' + b.t">
            <template v-for="(t, j) in b.inl">
              <a v-if="t.t === 'link'" :key="'a' + j" :href="t.href" target="_blank" rel="noopener noreferrer">{{ t.s }}</a>
              <strong v-else-if="t.t === 'bold'" :key="'b' + j">{{ t.s }}</strong>
              <span v-else :key="'s' + j">{{ t.s }}</span>
            </template>
          </div>
        </div>
      </div>

      <!-- 安装包下载（仅在有新版本时提供） -->
      <div v-if="hasUpdate && zipAsset" class="upd-dl">
        <div class="upd-dl-row">
          <span class="upd-asset">{{ zipAsset.name }}（{{ fmtSize(zipAsset.size) }}）</span>
          <button
            class="upd-dl-btn"
            :disabled="download && download.active"
            @click="upd.startDownload()"
          >
            <IconDownload /> {{ download && download.active ? '下载中…' : '下载新版本' }}
          </button>
        </div>

        <!-- 进度条：下载激活时显示 -->
        <div v-if="download && download.active" class="upd-progress">
          <div class="upd-progress-bar"><div class="upd-progress-fill" :style="{ width: percent + '%' }" /></div>
          <span class="upd-progress-text">{{ percent }}%（{{ fmtSize(download.received) }} / {{ fmtSize(download.total) }}）</span>
        </div>

        <!-- 完成态：保存路径 + 定位 -->
        <template v-if="download && download.done && download.savePath && !download.error">
          <div class="upd-done-path">已保存到：{{ download.savePath }}</div>
          <div class="upd-done-row">
            <button class="upd-mini-btn" @click="upd.revealDownload()"><IconFolderOpen /> 打开所在文件夹</button>
            <span v-if="s.revealError" class="upd-inline-err">{{ s.revealError }}</span>
          </div>
        </template>
        <div v-if="download && download.error" class="upd-inline-err">下载失败：{{ download.error }}</div>
        <div v-if="s.downloadError" class="upd-inline-err">{{ s.downloadError }}</div>
      </div>

      <!-- 底部操作 -->
      <div class="upd-foot">
        <button class="upd-mini-btn" :disabled="s.checking" @click="upd.manualCheck()">
          <IconRefresh :class="{ spin: s.checking }" /> {{ s.checking ? '检查中…' : '重新检查' }}
        </button>
        <a
          v-if="latest && latest.url"
          class="upd-mini-btn link"
          :href="latest.url"
          target="_blank"
          rel="noopener noreferrer"
        >
          <IconExternalLink /> 前往发布页
        </a>
        <span class="upd-foot-grow" />
        <button class="upd-close-btn" @click="emit('close')">关闭</button>
      </div>
      <div v-if="s.lastChecked" class="upd-lastcheck">上次检测：{{ fmtDate(s.lastChecked) }}（每 3 分钟自动检测一次）</div>
    </div>
  </div>
</template>

<style scoped>
.upd-mask {
  position: fixed;
  inset: 0;
  z-index: 10002;
  background: rgba(0, 0, 0, 0.5);
  display: grid;
  place-items: center;
}
.upd-box {
  width: 500px;
  max-width: calc(100vw - 40px);
  max-height: calc(100vh - 80px);
  overflow-y: auto;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--win-shadow);
  padding: 14px 16px;
}

.upd-head { display: flex; align-items: center; margin-bottom: 10px; }
.upd-title {
  display: flex;
  align-items: center;
  gap: 7px;
  font-size: 15px;
  font-weight: 700;
  color: var(--shell-glow);
  font-family: var(--font-mono);
  flex: 1;
}
.upd-title-icon { width: 16px; height: 16px; }
.upd-close {
  display: grid;
  place-items: center;
  width: 24px;
  height: 24px;
  padding: 0;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
}
.upd-close svg { width: 14px; height: 14px; }
.upd-close:hover { background: rgba(150, 225, 255, 0.10); color: var(--text-0); }

.upd-versions { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.upd-ver { font-size: 12.5px; color: var(--text-1); }
.upd-ver b { font-family: var(--font-mono); color: var(--text-0); font-weight: 600; }
.upd-ver.new b { color: var(--accent-green); }
.upd-arrow { color: var(--text-2); }
.upd-newline { margin-top: 6px; font-size: 12.5px; color: var(--accent-green); }
.upd-idle { margin-top: 6px; font-size: 12.5px; color: var(--text-1); }
.upd-idle.ok { color: var(--accent-green); }
.upd-idle.err { color: var(--accent-red); }

/* ---- 更新说明 ---- */
.upd-notes { margin-top: 12px; }
.upd-notes-title { font-size: 12px; color: var(--text-2); margin-bottom: 5px; }
.upd-notes-body {
  background: var(--surface-0, rgba(0, 0, 0, 0.25));
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  padding: 10px 12px;
  max-height: 220px;
  overflow-y: auto;
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-1);
}
.nb-gap { height: 7px; }
.nb-h { font-weight: 700; color: var(--text-0); margin: 3px 0; }
.nb-p { margin: 2px 0; }
.nb-li { position: relative; padding-left: 14px; margin: 2px 0; }
.nb-li::before { content: '•'; position: absolute; left: 2px; color: var(--shell-glow); }
.upd-notes-body a {
  color: var(--shell-glow);
  text-decoration: none;
  word-break: break-all;
}
.upd-notes-body a:hover { text-decoration: underline; }
.upd-notes-body strong { color: var(--text-0); }

/* ---- 下载区 ---- */
.upd-dl {
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px solid var(--line);
}
.upd-dl-row { display: flex; align-items: center; gap: 10px; }
.upd-asset { flex: 1; min-width: 0; font-size: 12px; color: var(--text-1); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.upd-dl-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  background: var(--accent-blue-btn);
  border: none;
  border-radius: var(--radius-sm);
  color: #fff;
  font-size: 12px;
  padding: 6px 12px;
  cursor: pointer;
  flex-shrink: 0;
}
.upd-dl-btn svg { width: 13px; height: 13px; }
.upd-dl-btn:hover { filter: brightness(1.1); }
.upd-dl-btn:disabled { opacity: 0.6; cursor: default; }

.upd-progress { display: flex; align-items: center; gap: 10px; margin-top: 10px; }
.upd-progress-bar {
  flex: 1;
  height: 6px;
  background: rgba(150, 225, 255, 0.10);
  border-radius: 999px;
  overflow: hidden;
}
.upd-progress-fill {
  height: 100%;
  background: var(--accent-green);
  border-radius: 999px;
  transition: width 0.4s ease;
  box-shadow: 0 0 6px rgba(71, 212, 185, 0.6);
}
.upd-progress-text { font-size: 11.5px; color: var(--text-2); font-family: var(--font-mono); white-space: nowrap; }

.upd-done-path { margin-top: 10px; font-size: 11.5px; color: var(--text-1); font-family: var(--font-mono); word-break: break-all; }
.upd-done-row { display: flex; align-items: center; gap: 10px; margin-top: 6px; }
.upd-mini-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: rgba(150, 225, 255, 0.08);
  border: 1px solid rgba(150, 225, 255, 0.18);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 5px 10px;
  cursor: pointer;
  text-decoration: none;
}
.upd-mini-btn svg { width: 13px; height: 13px; color: var(--shell-glow); }
.upd-mini-btn:hover { background: rgba(150, 225, 255, 0.14); color: var(--text-0); }
.upd-mini-btn:disabled { opacity: 0.5; cursor: default; }
.upd-mini-btn.link { color: var(--shell-glow); }
.upd-inline-err { font-size: 11.5px; color: var(--accent-red); }

/* ---- 底部 ---- */
.upd-foot { display: flex; align-items: center; gap: 8px; margin-top: 14px; }
.upd-foot-grow { flex: 1; }
.upd-close-btn {
  background: var(--accent-blue-btn);
  border: none;
  border-radius: var(--radius-sm);
  color: #fff;
  font-size: 12px;
  padding: 6px 16px;
  cursor: pointer;
}
.upd-close-btn:hover { filter: brightness(1.1); }
.upd-lastcheck { margin-top: 8px; font-size: 11px; color: var(--text-2); text-align: right; }

.spin { animation: upd-spin 1s linear infinite; }
@keyframes upd-spin { to { transform: rotate(360deg); } }
</style>
