import { reactive } from 'vue';

/**
 * 图片裁切：全局单例。任意处 `cropper.open(file, opts)` 返回 Promise<dataURL|null>，
 * 模态框由 Desktop 挂载的 <CropperHost/> 渲染（一次挂载，全局可用）。
 *
 * 坐标系约定（显示与裁切共用，改一处必改另一处）：
 * - 舞台 stageW×stageH，选区 selW×selH 且与舞台同心；
 * - fit = 盖住选区的最小缩放 = max(selW/图宽, selH/图高)，即 scale=1 时图恰好
 *   盖满选区（正方形模式下等价于 BOX / min(图宽, 图高)，几何不变）；
 * - 显示层 transform = translate(-50%,-50%) translate(offset) scale(fit*scale)，
 *   图片中心始终落在 舞台中心+offset；
 * - 裁切 = 选区在图片自然坐标下的矩形：左上角相对图片中心 = (-selW/2 - offset)/k，
 *   边长 = selW/k × selH/k，k = fit*scale。
 * - 输出：头像/图标 = 256×256 PNG（shape 'square'|'circle'，保留透明度）；
 *   壁纸 = shape 'free' + aspect（宽高比）+ output.longEdge（最长边像素），
 *   输出 JPEG（quality 可配）。
 * - 所见即所得：free 模式舞台与选区同尺寸（舞台即裁切框），弹窗里可见的画面
 *   就是最终成图——否则舞台里展示的"整图"与上墙后的选区子区域观感割裂
 *   （实测反馈：裁切显示和实际效果完全不一致的根因）。
 */
export const STAGE = 320;
export const BOX = 240;
const OUTPUT_SIZE = 256;

const state = reactive({
  visible: false,
  title: '裁切图片',
  shape: 'square',          // 'square' | 'circle' | 'free'
  imgSrc: null,
  imgW: 0, imgH: 0,
  fit: 1,                   // 基准缩放：恰好盖住选区
  scale: 1,                 // 用户缩放倍率（≥1）
  offsetX: 0, offsetY: 0,   // 图片中心相对舞台中心的偏移
  stageW: STAGE, stageH: STAGE,
  selW: BOX, selH: BOX,     // 选区尺寸（显示坐标，与舞台同心）
  outW: OUTPUT_SIZE, outH: OUTPUT_SIZE,
  mime: 'image/png',
  quality: undefined,
  resolve: null,
  fileName: '',
});

/** 当前有效缩放 k = fit × scale（显示层与裁切共用的唯一比例）。 */
export function effectiveScale() {
  return state.fit * state.scale;
}

export function useCropper() {
  /**
   * opts：
   * - title 弹窗标题；shape 'square'|'circle'|'free'（free=矩形选区）；
   * - aspect 选区宽高比（仅 free；缺省按舞台内最大矩形取 1）；
   * - output { longEdge, mime, quality }：输出图最长边像素与格式
   *   （缺省 256×256 PNG，即头像/图标形态）。
   */
  function open(file, { title = '裁切图片', shape = 'square', aspect = 1, output = null } = {}) {
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onerror = () => resolve(null);
      reader.onload = () => {
        const img = new Image();
        img.onerror = () => resolve(null);
        img.onload = () => {
          const free = shape === 'free';
          // free 模式：舞台即选区（长边 STAGE，按 aspect 出矩形）——所见即所得；
          // 方框/圆圈模式维持原几何（舞台 STAGE、选区 BOX）
          const a = free && aspect > 0 ? aspect : 1;
          if (free) {
            if (a >= 1) {
              state.selW = STAGE;
              state.selH = Math.round(STAGE / a);
            } else {
              state.selH = STAGE;
              state.selW = Math.round(STAGE * a);
            }
            state.stageW = state.selW;
            state.stageH = state.selH;
          } else {
            state.selW = BOX;
            state.selH = BOX;
            state.stageW = STAGE;
            state.stageH = STAGE;
          }
          const longEdge = output?.longEdge ?? OUTPUT_SIZE;
          if (state.selW >= state.selH) {
            state.outW = longEdge;
            state.outH = Math.max(1, Math.round(longEdge * state.selH / state.selW));
          } else {
            state.outH = longEdge;
            state.outW = Math.max(1, Math.round(longEdge * state.selW / state.selH));
          }
          state.mime = output?.mime || 'image/png';
          state.quality = output?.quality;
          state.title = title;
          state.shape = shape;
          state.imgSrc = String(reader.result);
          state.imgW = img.width;
          state.imgH = img.height;
          state.fit = Math.max(state.selW / img.width, state.selH / img.height);
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
      // 选区左上角相对图片中心 = (-selW/2 - offset)/k（与显示层同一套 k 与 offset）
      const sx = state.imgW / 2 + (-state.selW / 2 - state.offsetX) / k;
      const sy = state.imgH / 2 + (-state.selH / 2 - state.offsetY) / k;
      const sw = state.selW / k;
      const sh = state.selH / k;

      const canvas = document.createElement('canvas');
      canvas.width = state.outW;
      canvas.height = state.outH;
      const ctx = canvas.getContext('2d');
      if (state.shape === 'circle') {
        ctx.beginPath();
        ctx.arc(state.outW / 2, state.outH / 2, Math.min(state.outW, state.outH) / 2, 0, Math.PI * 2);
        ctx.clip();
      }
      ctx.drawImage(img, sx, sy, sw, sh, 0, 0, state.outW, state.outH);
      resolve(canvas.toDataURL(state.mime, state.quality));
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
  const maxX = Math.max(0, (drawW - state.selW) / 2);
  const maxY = Math.max(0, (drawH - state.selH) / 2);
  state.offsetX = Math.min(maxX, Math.max(-maxX, state.offsetX));
  state.offsetY = Math.min(maxY, Math.max(-maxY, state.offsetY));
  // 缩放下限：短边也要能盖住选区（fit 已保证 scale=1 时恰好盖住，这里兜底）
  const minScale = Math.max(state.selW / state.imgW, state.selH / state.imgH) / state.fit;
  state.scale = Math.max(Math.max(1, minScale), Math.min(8, state.scale));
}
