import { reactive, readonly } from 'vue';
import { API, createPoller } from '../api.js';

const state = reactive({
  loaded: false,
  checking: false,
  current: '',
  hasUpdate: false,
  latest: null,
  lastChecked: null,
  error: null,
  download: null,
  downloadError: null,
  revealError: null,
  bubbleDismissed: false,
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
  if (state.download && state.download.active) watchDownload();
}

async function refresh() {
  const d = await API.updateStatus();
  if (!d || d.offline) return;
  apply(d);
}

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

async function startDownload() {
  const d = await API.updateDownload();
  if (!d || d.offline) return;
  if (d.ok === false) {
    state.downloadError = d.error || 'download failed';
    return;
  }
  state.downloadError = null;
  await refresh();
}

async function revealDownload() {
  const d = await API.updateReveal();
  state.revealError = (!d || d.offline) ? 'network error' : (d.ok === false ? (d.error || 'reveal failed') : null);
}

function dismissBubble() {
  state.bubbleDismissed = true;
}

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
