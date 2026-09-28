import { reactive } from 'vue';

/**
 * 窗口管理：开/关/聚焦/最小化/位置尺寸持久化（sessionStorage，随标签页会话，
 * 不用 localStorage——桌面布局是"这次用起来顺手"的临时状态，不需要跨会话保留，
 * 避免窗口记在很旧的视口尺寸上开出屏幕外还得自己想办法找回来）。
 *
 * 多实例：同一 appId 可开多扇窗口（如多个终端），id 用 appId+序号唯一；
 * 布局按 appId 记最后一扇窗口的几何（下次新开沿用，避免记一堆旧坐标）。
 */
const STORAGE_KEY = 'dt_desktop_windows_v1';

const windows = reactive([]);   // [{ id, appId, title, x, y, w, h, z, minimized, maximized, preMax }]
let zCounter = 10;
let instanceSeq = {};           // appId -> 已开实例数（本次会话）

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
  // 移动/缩放会高频触发，300ms 防抖合并写
  if (saveTimer) return;
  saveTimer = setTimeout(() => {
    saveTimer = null;
    try {
      const layout = {};
      for (const w of windows) {
        layout[w.appId] = { x: w.x, y: w.y, w: w.w, h: w.h };
      }
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(layout));
    } catch { /* 隐私模式等场景忽略 */ }
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
    // 多实例级联偏移，避免标题全遮在一起
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

  /** 任务栏标签点击：未聚焦→聚焦；已聚焦→最小化（Windows 习惯）。 */
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

  return { windows, open, close, focus, minimize, toggleFocus, toggleMaximize, updateGeometry };
}
