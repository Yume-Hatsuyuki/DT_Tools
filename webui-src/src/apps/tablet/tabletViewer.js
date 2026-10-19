/**
 * 照片查看器的几何计算。纯函数，不依赖 DOM，便于单元测试。
 *
 * 坐标约定：
 * - 原点为视口中心，x 向右、y 向下（与 CSS 一致）。
 * - view = { scale, tx, ty }：scale 相对图片原始像素（1 = 原始大小），
 *   (tx, ty) 为图片中心相对视口中心的偏移。
 * - geom = { iw, ih, vw, vh }：图片原始宽高与视口宽高。
 */

/** 放大上限（相对原始像素）。 */
export const MAX_SCALE = 4;
/** 双击放大的目标（相对原始像素）。 */
export const DOUBLE_TAP_SCALE = 2;

const valid = (n) => Number.isFinite(n) && n > 0;

/** 适应窗口：完整落入视口，且不放大（小图按原始大小显示）。几何无效时退回 1。 */
export function fitScale(iw, ih, vw, vh) {
  if (!(valid(iw) && valid(ih) && valid(vw) && valid(vh))) return 1;
  return Math.min(vw / iw, vh / ih, 1);
}

/** 缩放范围：下限为适应窗口，上限为 MAX_SCALE（若适应值更大则以适应值为上限）。 */
export function scaleLimits(geom) {
  const min = fitScale(geom.iw, geom.ih, geom.vw, geom.vh);
  return { min, max: Math.max(MAX_SCALE, min) };
}

export function clampScale(scale, min, max) {
  if (!Number.isFinite(scale)) return min;
  return Math.min(max, Math.max(min, scale));
}

/**
 * 平移范围：图片比视口小的方向锁定居中；比视口大的方向只能平移到边缘，
 * 不允许露出视口之外的空白。
 */
export function clampPan(tx, ty, scale, geom) {
  const maxX = Math.max(0, (geom.iw * scale - geom.vw) / 2);
  const maxY = Math.max(0, (geom.ih * scale - geom.vh) / 2);
  // + 0 把 -0 归一为 0，避免样式字符串里出现 "-0px"
  return {
    tx: Math.min(maxX, Math.max(-maxX, tx)) + 0,
    ty: Math.min(maxY, Math.max(-maxY, ty)) + 0,
  };
}

/** 初始视图：适应窗口、居中。 */
export function initialView(geom) {
  return { scale: fitScale(geom.iw, geom.ih, geom.vw, geom.vh), tx: 0, ty: 0 };
}

/**
 * 以视口内某点为锚缩放（cx, cy 为该点相对视口中心的坐标）。
 * 锚点下方的图像内容缩放前后保持在同一屏幕位置，这是鼠标滚轮与双指缩放的体验基础。
 */
export function zoomAt(view, factor, cx, cy, geom) {
  if (!(view.scale > 0)) return initialView(geom);
  const { min, max } = scaleLimits(geom);
  const scale = clampScale(view.scale * factor, min, max);
  const ratio = scale / view.scale;
  const tx = cx - (cx - view.tx) * ratio;
  const ty = cy - (cy - view.ty) * ratio;
  return { scale, ...clampPan(tx, ty, scale, geom) };
}

/** 双击：适应窗口时放大到 DOUBLE_TAP_SCALE，否则回到适应窗口。 */
export function toggleView(view, cx, cy, geom) {
  const { min } = scaleLimits(geom);
  if (Math.abs(view.scale - min) < 1e-3) {
    const target = Math.min(scaleLimits(geom).max, DOUBLE_TAP_SCALE);
    return zoomAt(view, target / view.scale, cx, cy, geom);
  }
  return initialView(geom);
}

/** 滚轮缩放倍率：向上滚放大，向下滚缩小；单次变化限制在 0.5–2 倍之间。 */
export function wheelFactor(deltaY) {
  return Math.min(2, Math.max(0.5, Math.exp(-deltaY * 0.0015)));
}

/** 双指两点的距离与中点（客户端坐标）。 */
export function pinchMetrics(p1, p2) {
  return {
    dist: Math.hypot(p2.x - p1.x, p2.y - p1.y),
    cx: (p1.x + p2.x) / 2,
    cy: (p1.y + p2.y) / 2,
  };
}
