import { reactive, ref } from 'vue';

/**
 * 窗口管理：开/关/聚焦/最小化/位置尺寸持久化（sessionStorage，随标签页会话，
 * 不用 localStorage——桌面布局是"这次用起来顺手"的临时状态，不需要跨会话保留，
 * 避免窗口记在很旧的视口尺寸上开出屏幕外还得自己想办法找回来）。
 */
const STORAGE_KEY = 'dt_desktop_windows_v1';

const windows = reactive([]);   // [{ id, appId, title, x, y, w, h, z, minimized, maximized }]
let zCounter = 10;

function loadLayout() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

function saveLayout() {
  try {
    const layout = {};
    for (const w of windows) {
      layout[w.appId] = { x: w.x, y: w.y, w: w.w, h: w.h };
    }
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(layout));
  } catch { /* 隐私模式等场景忽略 */ }
}

function defaultGeometry(appId, defaults) {
  const saved = loadLayout()[appId];
  if (saved) return saved;
  return defaults || { x: 80 + windows.length * 28, y: 60 + windows.length * 24, w: 720, h: 480 };
}

export function useWindowManager() {
  function open(appId, { title, icon, defaults } = {}) {
    const existing = windows.find(w => w.appId === appId);
    if (existing) {
      existing.minimized = false;
      focus(existing.id);
      return existing;
    }
    const geo = defaultGeometry(appId, defaults);
    const win = reactive({
      id: appId + ':' + Date.now(),
      appId,
      title: title || appId,
      icon: icon || null,
      x: geo.x, y: geo.y, w: geo.w, h: geo.h,
      z: ++zCounter,
      minimized: false,
      maximized: false,
      preMax: null,
    });
    windows.push(win);
    return win;
  }

  function close(id) {
    const idx = windows.findIndex(w => w.id === id);
    if (idx !== -1) windows.splice(idx, 1);
    saveLayout();
  }

  function focus(id) {
    const w = windows.find(w => w.id === id);
    if (!w) return;
    w.z = ++zCounter;
    w.minimized = false;
  }

  function minimize(id) {
    const w = windows.find(w => w.id === id);
    if (w) w.minimized = true;
  }

  function toggleMaximize(id) {
    const w = windows.find(w => w.id === id);
    if (!w) return;
    if (w.maximized) {
      Object.assign(w, w.preMax);
      w.maximized = false;
    } else {
      w.preMax = { x: w.x, y: w.y, w: w.w, h: w.h };
      w.maximized = true;
    }
  }

  function updateGeometry(id, patch) {
    const w = windows.find(w => w.id === id);
    if (!w) return;
    Object.assign(w, patch);
  }

  function persist() {
    saveLayout();
  }

  return { windows, open, close, focus, minimize, toggleMaximize, updateGeometry, persist };
}
