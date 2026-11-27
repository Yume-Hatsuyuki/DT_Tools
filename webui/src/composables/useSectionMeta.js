import { reactive } from 'vue';

const STORAGE_KEY = 'dt_section_meta_v1';
const ICON_MAX_BYTES = 256 * 1024;

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
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(meta)); } catch { }
}

export function useSectionMeta() {
  function get(key) {
    return meta[key] || null;
  }

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
