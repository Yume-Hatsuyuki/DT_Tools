import { reactive } from 'vue';

/**
 * "文件夹"备忘录元数据：仅 WebUI 层的显示重命名 + 自定义图标，
 * 不触碰后端配置（段名/模块 id 始终是原始键）。
 * 键 = 段名（配置段与自动化模块共用一套键，二者本就指向同一配置段）。
 * 结构：{ "<段名>": { name?: "显示名", icon?: "dataURL" } }
 */
const STORAGE_KEY = 'dt_section_meta_v1';
const ICON_MAX_BYTES = 256 * 1024;   // 裁切后 256×256，正常远小于此；防超大 PNG 撑爆配额

const meta = reactive(load());

function load() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

function persist() {
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(meta)); } catch { /* 隐私模式忽略 */ }
}

export function useSectionMeta() {
  function get(key) {
    return meta[key] || null;
  }

  /** 备忘录级显示名（未设置时返回 null，调用方回退原名）。 */
  function displayName(key) {
    const m = meta[key];
    return m && m.name ? m.name : null;
  }

  function setName(key, name) {
    const n = String(name ?? '').trim();
    if (!meta[key] && !n) return;
    if (!meta[key]) meta[key] = {};
    if (n) meta[key].name = n;
    else delete meta[key].name;
    if (!meta[key].name && !meta[key].icon) delete meta[key];
    persist();
  }

  function setIcon(key, dataUrl) {
    if (!dataUrl) return clearIcon(key);
    if (dataUrl.length > ICON_MAX_BYTES) return { error: '图片处理后仍过大，请换小一些的图' };
    if (!meta[key]) meta[key] = {};
    meta[key].icon = dataUrl;
    persist();
    return null;
  }

  function clearIcon(key) {
    if (!meta[key]) return;
    delete meta[key].icon;
    if (!meta[key].name) delete meta[key];
    persist();
  }

  return { get, displayName, setName, setIcon, clearIcon };
}
