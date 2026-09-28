import { reactive } from 'vue';

/**
 * 图片裁切：全局单例。任意处 `cropper.open(file, opts)` 返回 Promise<dataURL|null>，
 * 模态框由 Desktop 挂载的 <CropperHost/> 渲染（一次挂载，全局可用）。
 *
 * 坐标系约定（显示与裁切共用，改一处必改另一处）：
 * - 舞台 STAGE×STAGE，选区 BOX×BOX 且与舞台同心；
 * - fit = BOX / min(图宽, 图高)，即 scale=1 时图短边恰好贴合选区；
 * - 显示层 transform = translate(-50%,-50%) translate(offset) scale(fit*scale)，
 *   图片中心始终落在 舞台中心+offset；
 * - 裁切 = 选区在图片自然坐标下的正方形：左上角相对图片中心 = (-BOX/2 - offset)/k，
 *   边长 = BOX/k，k = fit*scale。输出统一 256×256 PNG（保留透明度）。
 */
export const STAGE = 320;
export const BOX = 240;
const OUTPUT_SIZE = 256;

const state = reactive({
  visible: false,
  title: '裁切图片',
  shape: 'square',          // 'square' | 'circle'
  imgSrc: null,
  imgW: 0, imgH: 0,
  fit: 1,                   // 基准缩放：短边贴合选区
  scale: 1,                 // 用户缩放倍率（≥1）
  offsetX: 0, offsetY: 0,   // 图片中心相对舞台中心的偏移
  resolve: null,
  fileName: '',
});

/** 当前有效缩放 k = fit × scale（显示层与裁切共用的唯一比例）。 */
export function effectiveScale() {
  return state.fit * state.scale;
}

export function useCropper() {
  function open(file, { title = '裁切图片', shape = 'square' } = {}) {
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onerror = () => resolve(null);
      reader.onload = () => {
        const img = new Image();
        img.onerror = () => resolve(null);
        img.onload = () => {
          state.title = title;
          state.shape = shape;
          state.imgSrc = String(reader.result);
          state.imgW = img.width;
          state.imgH = img.height;
          state.fit = BOX / Math.min(img.width, img.height);
          state.scale = 1;
          state.offsetX = 0;
          state.offsetY = 0;
          state.fileName = file?.name || '';
          state.resolve = resolve;
          state.visible = true;
        };
        img.src = String(reader.result);
      };
      reader.readAsDataURL(file);
    });
  }

  return { state, open };
}

/** 把当前视口状态渲染到离屏 canvas 并导出 dataURL。 */
export function cropToDataUrl() {
  const img = new Image();
  return new Promise((resolve) => {
    img.onload = () => {
      const k = effectiveScale();
      // 源矩形以图片左上角为原点：图片中心在自然坐标 (imgW/2, imgH/2)，
      // 选区左上角相对图片中心 = (-BOX/2 - offset)/k（与显示层同一套 k 与 offset）
      const sx = state.imgW / 2 + (-BOX / 2 - state.offsetX) / k;
      const sy = state.imgH / 2 + (-BOX / 2 - state.offsetY) / k;
      const sw = BOX / k;

      const canvas = document.createElement('canvas');
      canvas.width = OUTPUT_SIZE;
      canvas.height = OUTPUT_SIZE;
      const ctx = canvas.getContext('2d');
      if (state.shape === 'circle') {
        ctx.beginPath();
        ctx.arc(OUTPUT_SIZE / 2, OUTPUT_SIZE / 2, OUTPUT_SIZE / 2, 0, Math.PI * 2);
        ctx.clip();
      }
      ctx.drawImage(img, sx, sy, sw, sw, 0, 0, OUTPUT_SIZE, OUTPUT_SIZE);
      resolve(canvas.toDataURL('image/png'));
    };
    img.onerror = () => resolve(null);
    img.src = state.imgSrc;
  });
}

/** 拖动/滚轮的边界钳制：图片必须始终盖住选区，不允许露底。 */
export function clampOffset() {
  const k = effectiveScale();
  const drawW = state.imgW * k;
  const drawH = state.imgH * k;
  const maxX = Math.max(0, (drawW - BOX) / 2);
  const maxY = Math.max(0, (drawH - BOX) / 2);
  state.offsetX = Math.min(maxX, Math.max(-maxX, state.offsetX));
  state.offsetY = Math.min(maxY, Math.max(-maxY, state.offsetY));
  // 缩放下限：短边也要能盖住选区（fit 已保证 scale=1 时恰好贴合，这里兜底）
  const minScale = BOX / Math.min(state.imgW, state.imgH) / state.fit;
  state.scale = Math.max(Math.max(1, minScale), Math.min(8, state.scale));
}
