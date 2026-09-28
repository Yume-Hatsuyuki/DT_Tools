import { reactive } from 'vue';

/**
 * 自定义图标：用户可为每个"应用"导入一张图片替换默认 Tabler 图标。
 * 存 localStorage（跟窗口布局的 sessionStorage 不同——图标是"我特意设置的外观"，
 * 应该跨会话保留，不应该随标签页关闭而丢）。图片转 dataURL 存储，单张限制
 * 512KB 原始文件大小，避免把 localStorage 塞爆（多数浏览器 5-10MB 上限）。
 * 不导入的应用图标位保持 null，由调用方回退到 Tabler 默认图标。
 */
const STORAGE_KEY = 'dt_desktop_custom_icons_v1';
const MAX_BYTES = 512 * 1024;

const icons = reactive(loadAll());

function loadAll() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

function persist() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(icons));
  } catch {
    // 超出配额等情况：不阻断功能，仅本次导入不落盘
  }
}

export function useCustomIcons() {
  function get(appId) {
    return icons[appId] || null;
  }

  function set(appId, dataUrl) {
    icons[appId] = dataUrl;
    persist();
  }

  function clear(appId) {
    delete icons[appId];
    persist();
  }

  /** 弹出系统图片选择框，读取为 dataURL 并保存；用户取消时静默返回。 */
  function pickAndSet(appId) {
    return new Promise((resolve) => {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/png,image/jpeg,image/gif,image/webp,image/svg+xml';
      input.onchange = () => {
        const file = input.files && input.files[0];
        if (!file) { resolve(false); return; }
        if (file.size > MAX_BYTES) {
          resolve({ error: `图片过大（${(file.size / 1024).toFixed(0)}KB），限 ${MAX_BYTES / 1024}KB 以内` });
          return;
        }
        const reader = new FileReader();
        reader.onload = () => {
          set(appId, String(reader.result));
          resolve(true);
        };
        reader.onerror = () => resolve({ error: '读取图片失败' });
        reader.readAsDataURL(file);
      };
      input.click();
    });
  }

  return { icons, get, set, clear, pickAndSet };
}
