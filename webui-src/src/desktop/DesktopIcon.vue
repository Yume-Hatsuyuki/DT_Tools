<script setup>
defineProps({
  label: { type: String, required: true },
  icon: { type: [Object, Function], default: null },   // Tabler 组件（默认图标）
  customSrc: { type: String, default: null },           // 用户导入的图片（优先于 icon）
  selected: { type: Boolean, default: false },
});
const emit = defineEmits(['open', 'select']);
</script>

<template>
  <button
    class="desktop-icon"
    :class="{ selected }"
    type="button"
    @click="emit('select')"
    @dblclick="emit('open')"
  >
    <span class="icon-tile">
      <img v-if="customSrc" :src="customSrc" :alt="label" class="icon-img" />
      <component :is="icon" v-else-if="icon" class="icon-fallback" />
    </span>
    <span class="icon-label">{{ label }}</span>
  </button>
</template>

<style scoped>
.desktop-icon {
  width: 92px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 10px 4px 8px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  cursor: pointer;
  -webkit-tap-highlight-color: transparent;
}
.desktop-icon:hover { background: rgba(39, 127, 255, 0.08); }
.desktop-icon.selected {
  background: rgba(39, 127, 255, 0.13);
  border-color: rgba(39, 127, 255, 0.35);
}

.icon-tile {
  width: 52px;
  height: 52px;
  display: grid;
  place-items: center;
  border-radius: 12px;
  /* 中间淡蓝向外扩散加深到深蓝，瓦片在亮壁纸上靠暗边缘与投影立住 */
  background: radial-gradient(circle at 50% 46%,
    rgba(110, 168, 255, 0.92) 0%,
    rgba(64, 118, 228, 0.88) 42%,
    rgba(24, 46, 104, 0.92) 82%,
    rgba(12, 24, 58, 0.95) 100%);
  border: 1px solid rgba(255, 255, 255, 0.18);
  box-shadow: 0 8px 20px rgba(4, 10, 28, 0.55), 0 1px 3px rgba(4, 10, 28, 0.5);
  transition: box-shadow 0.15s var(--ease), border-color 0.15s var(--ease);
}
.desktop-icon:hover .icon-tile,
.desktop-icon.selected .icon-tile {
  border-color: rgba(255, 255, 255, 0.45);
  box-shadow: 0 10px 26px rgba(4, 10, 28, 0.65), 0 0 0 1px rgba(39, 127, 255, 0.3);
}
.icon-fallback { width: 26px; height: 26px; color: #fff; filter: drop-shadow(0 1px 2px rgba(4, 10, 28, 0.8)); }
.icon-img { width: 100%; height: 100%; object-fit: cover; border-radius: 12px; }

.icon-label {
  font-size: 11.5px;
  color: var(--text-1);
  text-align: center;
  line-height: 1.3;
  text-shadow:
    0 1px 3px rgba(3, 8, 22, 0.95),
    0 0 6px rgba(3, 8, 22, 0.75),
    0 0 12px rgba(3, 8, 22, 0.5);
  word-break: break-word;
}
.desktop-icon.selected .icon-label,
.desktop-icon:hover .icon-label { color: var(--text-0); }
</style>
