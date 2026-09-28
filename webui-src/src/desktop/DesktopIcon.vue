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
.desktop-icon:hover { background: rgba(0, 229, 255, 0.05); }
.desktop-icon.selected {
  background: rgba(0, 229, 255, 0.09);
  border-color: rgba(0, 229, 255, 0.25);
}

.icon-tile {
  width: 52px;
  height: 52px;
  display: grid;
  place-items: center;
  border-radius: 12px;
  background: linear-gradient(160deg, rgba(0, 229, 255, 0.1), rgba(255, 45, 149, 0.06));
  border: 1px solid rgba(255, 255, 255, 0.06);
}
.icon-fallback { width: 26px; height: 26px; color: var(--accent-cyan); }
.icon-img { width: 100%; height: 100%; object-fit: cover; border-radius: 12px; }

.icon-label {
  font-size: 11.5px;
  color: var(--text-1);
  text-align: center;
  line-height: 1.3;
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.8);
  word-break: break-word;
}
.desktop-icon.selected .icon-label,
.desktop-icon:hover .icon-label { color: var(--text-0); }
</style>
