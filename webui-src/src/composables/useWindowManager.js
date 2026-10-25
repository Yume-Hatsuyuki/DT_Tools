import { reactive } from 'vue';

const STORAGE_KEY = 'dt_desktop_windows_v1';

const windows = reactive([]);
let zCounter = 10;
let instanceSeq = {};

function loadLayout() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

let saveTimer = null;
function saveLayout() {
  if (saveTimer) return;
  saveTimer = setTimeout(() => {
    saveTimer = null;
    try {
      const layout = {};
      for (const w of windows) {
        layout[w.appId] = { x: w.x, y: w.y, w: w.w, h: w.h };
      }
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(layout));
    } catch { }
  }, 300);
}

function defaultGeometry(appId, defaults) {
  const saved = loadLayout()[appId];
  if (saved) return saved;
  return defaults || { x: 80, y: 60, w: 720, h: 480 };
}

export function useWindowManager() {
  function open(appId, { title, icon, defaults } = {}) {
    instanceSeq[appId] = (instanceSeq[appId] || 0) + 1;
    const n = instanceSeq[appId];
    const geo = defaultGeometry(appId, defaults);
    const offset = (n - 1) * 28;
    const win = reactive({
      id: appId + ':' + Date.now() + ':' + n,
      appId,
      title: n > 1 ? `${title || appId} #${n}` : (title || appId),
      icon: icon || null,
      x: geo.x + offset,
      y: geo.y + offset,
      w: geo.w, h: geo.h,
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

  function toggleFocus(id) {
    const top = windows.reduce((a, b) => (!a || b.z > a.z ? b : a), null);
    const w = windows.find(w => w.id === id);
    if (!w) return;
    if (w.minimized) { focus(id); return; }
    if (top && top.id === id) minimize(id);
    else focus(id);
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
    saveLayout();
  }

  let preShow = null;

  function showDesktop() {
    const anyVisible = windows.some(w => !w.minimized);
    if (anyVisible) {
      preShow = new Map(windows.map(w => [w.id, w.minimized]));
      for (const w of windows) w.minimized = true;
    } else {
      for (const w of windows) w.minimized = preShow ? preShow.get(w.id) === true : false;
      preShow = null;
    }
  }

  function minimizeAll() {
    preShow = new Map(windows.map(w => [w.id, w.minimized]));
    for (const w of windows) w.minimized = true;
  }

  function closeAll() {
    preShow = null;
    windows.splice(0, windows.length);
    saveLayout();
  }

  return { windows, open, close, focus, minimize, toggleFocus, toggleMaximize, updateGeometry, showDesktop, minimizeAll, closeAll };
}
