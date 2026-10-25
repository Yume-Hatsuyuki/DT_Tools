import { reactive } from 'vue';

const STORAGE_KEY = 'dt_wallpaper_v1';
const DEFAULT_PRESET = 'abyss-flow';

const WALLPAPER_PRESETS = [
  {
    id: 'abyss-flow',
    label: '深海数据流',
    css: 'radial-gradient(1100px 640px at 50% -10%, rgba(126, 224, 255, 0.22), transparent 62%),'
       + ' radial-gradient(1500px 900px at 88% 12%, rgba(74, 178, 210, 0.15), transparent 55%),'
       + ' radial-gradient(1300px 800px at 6% 34%, rgba(46, 132, 172, 0.13), transparent 55%),'
       + ' linear-gradient(180deg, #0d2531 0%, #0a1c28 45%, #071019 100%)',
  },
  {
    id: 'kali-blue',
    label: 'Kali 蓝',
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

const state = reactive(load());

function persist() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    return true;
  } catch {
    return false;
  }
}

function rgbToHsl(r, g, b) {
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  if (max === min) return [0, 0, l];
  const d = max - min;
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
  let h;
  if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
  else if (max === g) h = ((b - r) / d + 2) / 6;
  else h = ((r - g) / d + 4) / 6;
  return [h * 360, s, l];
}

function dockTint(avg) {
  return {
    h: Math.round(avg.h),
    s: +Math.min(0.55, avg.s * 0.9).toFixed(3),
    l: +Math.min(0.24, Math.max(0.1, avg.l * 0.55)).toFixed(3),
  };
}

function extractDockTint(dataUrl) {
  return new Promise((resolve) => {
    const img = new Image();
    img.onerror = () => resolve(null);
    img.onload = () => {
      try {
        const N = 48;
        const canvas = document.createElement('canvas');
        canvas.width = canvas.height = N;
        canvas.getContext('2d').drawImage(img, 0, 0, N, N);
        const d = canvas.getContext('2d').getImageData(0, 0, N, N).data;
        let r = 0, g = 0, b = 0, w = 0;
        for (let i = 0; i < d.length; i += 4) {
          const a = d[i + 3];
          if (!a) continue;
          r += d[i] * a; g += d[i + 1] * a; b += d[i + 2] * a; w += a;
        }
        if (!w) { resolve(null); return; }
        const [h, s, l] = rgbToHsl(r / w / 255, g / w / 255, b / w / 255);
        resolve(dockTint({ h, s, l }));
      } catch {
        resolve(null);
      }
    };
    img.src = dataUrl;
  });
}

export function useWallpaper() {
  if (!state.presetId || !WALLPAPER_PRESETS.some(p => p.id === state.presetId)) {
    state.presetId = DEFAULT_PRESET;
  }
  if (state.presetId === 'kali-blue' && !state.custom) {
    state.presetId = DEFAULT_PRESET;
    persist();
  }

  async function setCustom(dataUrl) {
    state.custom = dataUrl;
    state.tint = await extractDockTint(dataUrl);
    if (!persist()) {
      return { error: '已应用，但浏览器存储配额不足，刷新后将恢复默认壁纸' };
    }
    return null;
  }

  function reset() {
    state.presetId = DEFAULT_PRESET;
    state.custom = null;
    state.tint = null;
    persist();
  }

  return { state, presets: WALLPAPER_PRESETS, setCustom, reset };
}

if (state.custom && !state.tint) {
  extractDockTint(state.custom).then((t) => {
    if (!t) return;
    state.tint = t;
    persist();
  });
}
