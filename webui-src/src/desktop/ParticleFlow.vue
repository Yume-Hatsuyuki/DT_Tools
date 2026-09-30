<script setup>
import { onMounted, onUnmounted, ref } from 'vue';

/**
 * 「深海数据流」背景层：CSS 光柱 + 透视地面网格 + canvas 上升粒子流，
 * 复刻 NoriOS（os.inori.ai）参考图的水下光柱与漂浮微粒氛围。
 *
 * 分层职责：壁纸渐变在 .desktop 背景上，本组件绝对定位于其上、窗口之下
 * （z-index 0，pointer-events:none）——粒子永远漂浮，光柱与地面网格仅在
 * CSS 预设壁纸时显示（自定义图片壁纸时会被光效压住照片，故只留粒子）。
 *
 * 性能：粒子用预渲染发光 sprite（离屏 canvas 画一次径向渐变，之后只
 * drawImage，避免每粒子 shadowBlur）；rAF 驱动，标签页隐藏即暂停；
 * prefers-reduced-motion 时只画一帧静态。
 */
const props = defineProps({
  /** 光柱 + 地面网格（自定义图片壁纸时传 false，只保留粒子层）。 */
  decorative: { type: Boolean, default: true },
});

const cv = ref(null);

let ctx = null;
let raf = 0;
let running = false;
let particles = [];
let streaks = [];
let sprite = null;
let dpr = 1;
let width = 0;
let height = 0;
let lastT = 0;

const PARTICLE_DENSITY = 1 / 16000;   // 每像素面积粒子数（1080p ≈ 120）
const STREAK_COUNT = 6;

function makeSprite() {
  const s = document.createElement('canvas');
  s.width = s.height = 48;
  const g = s.getContext('2d');
  const grad = g.createRadialGradient(24, 24, 0, 24, 24, 24);
  grad.addColorStop(0, 'rgba(235, 252, 255, 0.95)');
  grad.addColorStop(0.25, 'rgba(160, 228, 248, 0.55)');
  grad.addColorStop(0.6, 'rgba(95, 217, 246, 0.14)');
  grad.addColorStop(1, 'rgba(95, 217, 246, 0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 48, 48);
  return s;
}

function spawnParticle(randomY) {
  const r = 0.6 + Math.random() * 1.8;
  return {
    x: Math.random() * width,
    y: randomY ? Math.random() * height : height + 8,
    r,
    // 上升数据流：速度与粒径弱相关，远景（小）更慢
    vy: 10 + r * 12 + Math.random() * 14,
    swayAmp: 6 + Math.random() * 18,
    swayFreq: 0.2 + Math.random() * 0.5,
    phase: Math.random() * Math.PI * 2,
    alpha: 0.2 + Math.random() * 0.55,
    twinkle: 0.5 + Math.random() * 2.2,
  };
}

function spawnStreak() {
  return {
    x: Math.random() * width,
    y: Math.random() * height,
    len: 36 + Math.random() * 70,
    vy: 180 + Math.random() * 220,
    alpha: 0.12 + Math.random() * 0.22,
  };
}

function resize() {
  dpr = Math.min(2, window.devicePixelRatio || 1);
  width = window.innerWidth;
  height = window.innerHeight;
  cv.value.width = Math.round(width * dpr);
  cv.value.height = Math.round(height * dpr);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

  const target = Math.round(width * height * PARTICLE_DENSITY);
  particles = Array.from({ length: target }, () => spawnParticle(true));
  streaks = Array.from({ length: STREAK_COUNT }, spawnStreak);
}

function step(t) {
  if (!running) return;
  const dt = Math.min(0.05, (t - lastT) / 1000 || 0.016);
  lastT = t;
  ctx.clearRect(0, 0, width, height);

  for (const p of particles) {
    p.y -= p.vy * dt;
    p.phase += p.twinkle * dt;
    const x = p.x + Math.sin(p.phase * p.swayFreq * 2) * p.swayAmp;
    if (p.y < -10) Object.assign(p, spawnParticle(false));
    const a = p.alpha * (0.65 + 0.35 * Math.sin(p.phase * 3));
    const size = p.r * 10;
    ctx.globalAlpha = Math.max(0, a);
    ctx.drawImage(sprite, x - size / 2, p.y - size / 2, size, size);
  }

  // 快速上升的"数据流"亮线：细长渐隐，偶尔划过
  ctx.lineWidth = 1.4;
  for (const s of streaks) {
    s.y -= s.vy * dt;
    if (s.y + s.len < -20) Object.assign(s, spawnStreak(), { y: height + 20 + Math.random() * 200 });
    const grad = ctx.createLinearGradient(s.x, s.y, s.x, s.y + s.len);
    grad.addColorStop(0, `rgba(190, 240, 255, 0)`);
    grad.addColorStop(0.5, `rgba(190, 240, 255, ${s.alpha})`);
    grad.addColorStop(1, `rgba(190, 240, 255, 0)`);
    ctx.strokeStyle = grad;
    ctx.beginPath();
    ctx.moveTo(s.x, s.y);
    ctx.lineTo(s.x, s.y + s.len);
    ctx.stroke();
  }

  ctx.globalAlpha = 1;
  raf = requestAnimationFrame(step);
}

function start() {
  if (running || !ctx) return;
  running = true;
  lastT = performance.now();
  raf = requestAnimationFrame(step);
}
function stop() {
  running = false;
  cancelAnimationFrame(raf);
}

function onVisibility() {
  if (document.hidden) stop();
  else if (!prefersReducedMotion()) start();
}

function prefersReducedMotion() {
  return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

onMounted(() => {
  ctx = cv.value.getContext('2d');
  sprite = makeSprite();
  resize();
  window.addEventListener('resize', resize);
  document.addEventListener('visibilitychange', onVisibility);
  if (prefersReducedMotion()) {
    // 静态一帧：粒子在场但不流动
    running = true;
    step(performance.now());
    running = false;
  } else {
    start();
  }
});
onUnmounted(() => {
  stop();
  window.removeEventListener('resize', resize);
  document.removeEventListener('visibilitychange', onVisibility);
});
</script>

<template>
  <div class="particle-flow" aria-hidden="true">
    <template v-if="decorative">
      <div class="beam beam-1" />
      <div class="beam beam-2" />
      <div class="beam beam-3" />
      <div class="grid-floor" />
    </template>
    <canvas ref="cv" class="particles" />
  </div>
</template>

<style scoped>
.particle-flow {
  position: absolute;
  inset: 0;
  z-index: 0;
  overflow: hidden;
  pointer-events: none;
}

/* 斜向水下光柱：缓慢平移 + 呼吸，营造"光从水面沉下来"的层次 */
.beam {
  position: absolute;
  top: -18%;
  height: 135%;
  width: 220px;
  background: linear-gradient(180deg,
    rgba(150, 228, 250, 0.16) 0%,
    rgba(110, 214, 245, 0.07) 45%,
    rgba(95, 217, 246, 0) 92%);
  filter: blur(26px);
  transform: rotate(16deg);
  transform-origin: top center;
  animation: beam-sway 26s var(--ease) infinite alternate;
}
.beam-1 { left: 12%; animation-duration: 30s; }
.beam-2 { left: 46%; width: 300px; opacity: 0.8; animation-duration: 22s; animation-delay: -8s; }
.beam-3 { left: 76%; width: 170px; opacity: 0.65; animation-duration: 34s; animation-delay: -16s; }
@keyframes beam-sway {
  from { transform: rotate(13deg) translateX(-36px); opacity: 0.7; }
  50% { opacity: 1; }
  to { transform: rotate(19deg) translateX(42px); opacity: 0.75; }
}

/* 地面透视网格：图2 底部的微亮地平线，向上渐隐 */
.grid-floor {
  position: absolute;
  left: -30%;
  right: -30%;
  bottom: -12%;
  height: 46%;
  background:
    linear-gradient(rgba(120, 214, 244, 0.10) 1px, transparent 1px),
    linear-gradient(90deg, rgba(120, 214, 244, 0.10) 1px, transparent 1px);
  background-size: 64px 44px;
  transform: perspective(520px) rotateX(58deg);
  transform-origin: bottom center;
  mask-image: linear-gradient(180deg, transparent 0%, rgba(0, 0, 0, 0.55) 60%, rgba(0, 0, 0, 0.9) 100%);
  -webkit-mask-image: linear-gradient(180deg, transparent 0%, rgba(0, 0, 0, 0.55) 60%, rgba(0, 0, 0, 0.9) 100%);
}

.particles {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}
</style>
