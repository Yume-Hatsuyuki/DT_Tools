import { reactive } from 'vue';

const STAGE = 320;
const BOX = 240;
const OUTPUT_SIZE = 256;

const state = reactive({
  visible: false,
  title: '裁切图片',
  shape: 'square',
  imgSrc: null,
  imgW: 0, imgH: 0,
  fit: 1,
  scale: 1,
  offsetX: 0, offsetY: 0,
  stageW: STAGE, stageH: STAGE,
  selW: BOX, selH: BOX,
  outW: OUTPUT_SIZE, outH: OUTPUT_SIZE,
  mime: 'image/png',
  quality: undefined,
  resolve: null,
  fileName: '',
});

function effectiveScale() {
  return state.fit * state.scale;
}

export function useCropper() {
  function open(file, { title = '裁切图片', shape = 'square', aspect = 1, output = null } = {}) {
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onerror = () => resolve(null);
      reader.onload = () => {
        const img = new Image();
        img.onerror = () => resolve(null);
        img.onload = () => {
          const free = shape === 'free';
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

export function cropToDataUrl() {
  const img = new Image();
  return new Promise((resolve) => {
    img.onload = () => {
      const k = effectiveScale();
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

export function clampOffset() {
  const k = effectiveScale();
  const drawW = state.imgW * k;
  const drawH = state.imgH * k;
  const maxX = Math.max(0, (drawW - state.selW) / 2);
  const maxY = Math.max(0, (drawH - state.selH) / 2);
  state.offsetX = Math.min(maxX, Math.max(-maxX, state.offsetX));
  state.offsetY = Math.min(maxY, Math.max(-maxY, state.offsetY));
  const minScale = Math.max(state.selW / state.imgW, state.selH / state.imgH) / state.fit;
  state.scale = Math.max(Math.max(1, minScale), Math.min(8, state.scale));
}
