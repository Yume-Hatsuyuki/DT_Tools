
export const MAX_SCALE = 4;
export const DOUBLE_TAP_SCALE = 2;

const valid = (n) => Number.isFinite(n) && n > 0;

export function fitScale(iw, ih, vw, vh) {
  if (!(valid(iw) && valid(ih) && valid(vw) && valid(vh))) return 1;
  return Math.min(vw / iw, vh / ih, 1);
}

export function scaleLimits(geom) {
  const min = fitScale(geom.iw, geom.ih, geom.vw, geom.vh);
  return { min, max: Math.max(MAX_SCALE, min) };
}

export function clampScale(scale, min, max) {
  if (!Number.isFinite(scale)) return min;
  return Math.min(max, Math.max(min, scale));
}

export function clampPan(tx, ty, scale, geom) {
  const maxX = Math.max(0, (geom.iw * scale - geom.vw) / 2);
  const maxY = Math.max(0, (geom.ih * scale - geom.vh) / 2);
  return {
    tx: Math.min(maxX, Math.max(-maxX, tx)) + 0,
    ty: Math.min(maxY, Math.max(-maxY, ty)) + 0,
  };
}

export function initialView(geom) {
  return { scale: fitScale(geom.iw, geom.ih, geom.vw, geom.vh), tx: 0, ty: 0 };
}

export function zoomAt(view, factor, cx, cy, geom) {
  if (!(view.scale > 0)) return initialView(geom);
  const { min, max } = scaleLimits(geom);
  const scale = clampScale(view.scale * factor, min, max);
  const ratio = scale / view.scale;
  const tx = cx - (cx - view.tx) * ratio;
  const ty = cy - (cy - view.ty) * ratio;
  return { scale, ...clampPan(tx, ty, scale, geom) };
}

export function toggleView(view, cx, cy, geom) {
  const { min } = scaleLimits(geom);
  if (Math.abs(view.scale - min) < 1e-3) {
    const target = Math.min(scaleLimits(geom).max, DOUBLE_TAP_SCALE);
    return zoomAt(view, target / view.scale, cx, cy, geom);
  }
  return initialView(geom);
}

export function wheelFactor(deltaY) {
  return Math.min(2, Math.max(0.5, Math.exp(-deltaY * 0.0015)));
}

export function pinchMetrics(p1, p2) {
  return {
    dist: Math.hypot(p2.x - p1.x, p2.y - p1.y),
    cx: (p1.x + p2.x) / 2,
    cy: (p1.y + p2.y) / 2,
  };
}
