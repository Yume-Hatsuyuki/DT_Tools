import { reactive, readonly } from 'vue';
import { API, createPoller } from '../api.js';

/**
 * 更新检测全局单例（与 useConnectionStatus 同款 store 式单例）：
 * - 3 分钟轮询 /api/update/status（后端同 TTL 缓存，多标签页共享，自动检测在页面隐藏时暂停）；
 * - 手动检查走 /api/update/check 强制刷新（关于弹窗的按钮、更新弹窗的重新检查共用）；
 * - 下载激活期间切换为 1 秒快轮询读进度（进度只在 /api/update/status 里回传）。
 * 顶栏更新徽标/气泡、关于弹窗的检查按钮、更新信息弹窗共用同一份状态。
 */
const state = reactive({
  loaded: false,          // 首次状态已到达
  checking: false,        // 手动检查进行中（按钮转圈）
  current: '',            // 插件当前版本（后端 csproj <Version>）
  hasUpdate: false,       // 最新 tag 比当前新
  latest: null,           // {tag,name,publishedAt,url,notes,assets:[{name,size,downloadUrl,downloadCount}]}
  lastChecked: null,      // 上次检测时间（本机时间字符串）
  error: null,            // 最近一次检测失败原因（成功时为 null）
  download: null,         // {active,done,received,total,savePath,error}
  downloadError: null,    // 发起下载被拒（如已在进行中）的内联提示
  revealError: null,      // "打开所在文件夹"失败的内联提示
  bubbleDismissed: false, // 顶栏气泡本轮页面会话内已手动关闭
});

let started = false;
let poller = null;
let dlTimer = null;

function apply(d) {
  state.loaded = true;
  state.current = d.current || '';
  state.hasUpdate = !!d.hasUpdate;
  state.latest = d.latest || null;
  state.lastChecked = d.lastChecked || null;
  state.error = d.error || null;
  state.download = d.download || null;
  // 后端下载在进行中（可能由另一个标签页启动）→ 本页也进入快轮询跟进度
  if (state.download && state.download.active) watchDownload();
}

async function refresh() {
  const d = await API.updateStatus();
  // 断网/掉线保持现状，不把本地状态洗成空
  if (!d || d.offline) return;
  apply(d);
}

/** 手动检查：绕过后端缓存立即请求 GitHub；结果照常落全局状态。 */
async function manualCheck() {
  if (state.checking) return;
  state.checking = true;
  try {
    const d = await API.updateCheck();
    if (d && !d.offline) apply(d);
  } finally {
    state.checking = false;
  }
}

/** 下载激活期间的 1 秒快轮询；结束时（done/error）自动停。 */
function watchDownload() {
  if (dlTimer) return;
  dlTimer = setInterval(async () => {
    const d = await API.updateStatus();
    if (d && !d.offline) apply(d);
    if (!state.download || !state.download.active) {
      clearInterval(dlTimer);
      dlTimer = null;
    }
  }, 1000);
}

/** 启动后台下载（发起新下载前重置完成态）；失败原因落 state.downloadError。 */
async function startDownload() {
  const d = await API.updateDownload();
  if (!d || d.offline) return;
  if (d.ok === false) {
    state.downloadError = d.error || 'download failed';
    return;
  }
  state.downloadError = null;
  // 立即拉一次状态拿到 active/total，再由 watchDownload 接管快轮询
  await refresh();
}

/** 资源管理器定位已下载的 zip；失败原因落 state.revealError。 */
async function revealDownload() {
  const d = await API.updateReveal();
  state.revealError = (!d || d.offline) ? 'network error' : (d.ok === false ? (d.error || 'reveal failed') : null);
}

function dismissBubble() {
  state.bubbleDismissed = true;
}

/** 桌面壳挂载时调用一次：立即拉取 + 进入 3 分钟轮询（幂等，多处调用只启动一份）。 */
function start() {
  if (started) return;
  started = true;
  refresh();
  poller = createPoller(refresh, 3 * 60 * 1000);
  poller.start();
}

export function useUpdateCheck() {
  return {
    state: readonly(state),
    start,
    manualCheck,
    startDownload,
    revealDownload,
    dismissBubble,
  };
}
