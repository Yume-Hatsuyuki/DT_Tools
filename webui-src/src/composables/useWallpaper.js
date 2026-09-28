import { reactive } from 'vue';

/**
 * 壁纸：默认 'kali-blue'——用 CSS 渐变复刻 Kali 官方分层壁纸的蓝紫配色，
 * 整体加深一档（保证深色图标瓦片在壁纸上可辨，避免"亮到看不见图标"）；
 * 另有其他纯 CSS 预设可切换；自定义图片先经 canvas 等比缩到最长边 1920、
 * JPEG q0.82 再落 localStorage，仍超配额时保内存态（本次会话生效）并提示。
 */
const STORAGE_KEY = 'dt_wallpaper_v1';
const MAX_EDGE = 1920;
const JPEG_QUALITY = 0.82;

export const WALLPAPER_PRESETS = [
  {
    id: 'kali-blue',
    label: 'Kali 蓝',
    // 复刻官方壁纸：左 2/3 中蓝、右侧偏青、左下一缕紫、斜向光带；整体亮蓝
    css: 'radial-gradient(1200px 800px at 96% 16%, rgba(84, 206, 216, 0.28), transparent 55%),'
       + ' radial-gradient(1400px 900px at 55% 0%, rgba(226, 246, 255, 0.16), transparent 55%),'
       + ' radial-gradient(1300px 900px at 0% 116%, rgba(108, 96, 235, 0.32), transparent 55%),'
       + ' linear-gradient(115deg, #3a74d6 0%, #3d7fd8 36%, #3e93d6 66%, #47a3d2 100%)',
  },
  { id: 'kali-night', label: '深蓝夜空', css: 'radial-gradient(1200px 600px at 15% -10%, rgba(39,127,255,0.16), transparent 55%), radial-gradient(900px 500px at 95% 8%, rgba(36,113,243,0.10), transparent 50%), linear-gradient(160deg, #0e1526 0%, #090d1a 60%, #070a13 100%)' },
  { id: 'kali-abyss', label: '深海', css: 'radial-gradient(1000px 700px at 50% 120%, rgba(39,127,255,0.20), transparent 60%), linear-gradient(180deg, #0a1020 0%, #0b1e3a 55%, #071022 100%)' },
  { id: 'kali-grid', label: '网格矩阵', css: 'linear-gradient(rgba(39,127,255,0.07) 1px, transparent 1px), linear-gradient(90deg, rgba(39,127,255,0.07) 1px, transparent 1px), radial-gradient(1200px 600px at 80% -10%, rgba(39,127,255,0.12), transparent 55%), linear-gradient(160deg, #0c1220, #080b14)', size: '42px 42px, 42px 42px, auto, auto' },
  { id: 'kali-aurora', label: '极光', css: 'radial-gradient(900px 500px at 20% 0%, rgba(71,212,185,0.10), transparent 55%), radial-gradient(1100px 600px at 85% 15%, rgba(39,127,255,0.16), transparent 55%), linear-gradient(180deg, #0b1220, #070a12)' },
];

function load() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
}

const state = reactive(load());   // { presetId, custom }

function persist() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    return true;
  } catch {
    return false;
  }
}

/** 把用户选择的图片文件压成 ≤1920px 的 JPEG dataURL。 */
function compressImage(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('读取图片失败'));
    reader.onload = () => {
      const img = new Image();
      img.onerror = () => reject(new Error('图片解码失败'));
      img.onload = () => {
        const scale = Math.min(1, MAX_EDGE / Math.max(img.width, img.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(img.width * scale));
        canvas.height = Math.max(1, Math.round(img.height * scale));
        canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
        resolve(canvas.toDataURL('image/jpeg', JPEG_QUALITY));
      };
      img.src = String(reader.result);
    };
    reader.readAsDataURL(file);
  });
}

export function useWallpaper() {
  // 首次使用/旧版本残留（已移除的图片预设）回退默认
  if (!state.presetId || !WALLPAPER_PRESETS.some(p => p.id === state.presetId)) {
    state.presetId = 'kali-blue';
  }

  function applyPreset(id) {
    state.presetId = id;
    state.custom = null;
    persist();
  }

  /** 设为自定义图片（File）；失败返回 { error }，成功返回 null。 */
  async function setCustom(file) {
    let dataUrl;
    try {
      dataUrl = await compressImage(file);
    } catch (e) {
      return { error: e.message || '图片处理失败' };
    }
    state.custom = dataUrl;
    if (!persist()) {
      // 配额超限：保内存态（本次会话仍生效）但告知不会跨会话保留
      return { error: '已应用，但浏览器存储配额不足，刷新后将恢复默认壁纸' };
    }
    return null;
  }

  function reset() {
    state.presetId = 'kali-blue';
    state.custom = null;
    persist();
  }

  return { state, presets: WALLPAPER_PRESETS, applyPreset, setCustom, reset };
}
