<script setup>
import { ref, watch, nextTick } from 'vue';

/**
 * 命令补全列表（两套控制台共用）：候选渲染 + 高亮项滚动跟随。
 * ↑↓ 高亮移出可视区时容器自动滚过去（旧实现只改高亮不滚动，
 * 长列表里选中的命令看不到——已确认缺陷的修复点）。
 * 定位（绝对锚点/宽度）由宿主的 .suggest-wrap 决定，本组件只画列表本身。
 */
const props = defineProps({
  /** [{name, aliases, usage, description, author}] */
  matches: { type: Array, required: true },
  /** 当前高亮下标。 */
  selIdx: { type: Number, required: true },
  /** 是否显示 / 前缀（旧版控制台带，终端不带）。 */
  showSlash: { type: Boolean, default: false },
});
const emit = defineEmits(['apply', 'hover']);

const listEl = ref(null);

watch(() => props.selIdx, async () => {
  await nextTick();
  const item = listEl.value && listEl.value.children[props.selIdx];
  if (item) item.scrollIntoView({ block: 'nearest' });
});
</script>

<template>
  <div ref="listEl" class="suggest">
    <div
      v-for="(c, i) in matches"
      :key="c.name"
      class="sug-item"
      :class="{ sel: i === selIdx }"
      @mousedown.prevent="emit('apply', i)"
      @mouseenter="emit('hover', i)"
    >
      <span class="sug-name" :class="{ builtin: c.builtin }">{{ showSlash ? '/' : '' }}{{ c.name }}</span>
      <span v-if="c.builtin" class="sug-tag">内置</span>
      <span class="sug-desc">{{ c.description || c.usage || '' }}</span>
      <span v-if="c.author" class="sug-author">功能制作者：{{ c.author }}</span>
    </div>
  </div>
</template>

<style scoped>
.suggest {
  max-height: 260px;
  overflow-y: auto;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-bottom: none;
  border-radius: var(--radius-sm) var(--radius-sm) 0 0;
  box-shadow: var(--win-shadow);
}
.sug-item { padding: 7px 12px; cursor: pointer; border-bottom: 1px solid var(--line); }
.sug-item:last-child { border-bottom: none; }
.sug-item.sel, .sug-item:hover { background: var(--surface-2); }
.sug-name { font-family: var(--font-mono); color: var(--accent-cyan); font-size: 12.5px; margin-right: 8px; }
/* 终端内置命令：白色名 + 「内置」徽标，与服务端命令（青色）区分开 */
.sug-name.builtin { color: var(--text-0); }
.sug-tag {
  font-size: 9.5px;
  line-height: 1;
  color: var(--text-2);
  border: 1px solid var(--line-strong);
  border-radius: 4px;
  padding: 2px 4px;
  margin-right: 8px;
  flex-shrink: 0;
}
.sug-desc { font-size: 11.5px; color: var(--text-1); }
.sug-author { display: block; font-size: 10.5px; color: var(--text-2); margin-top: 2px; }
</style>
