<script setup>
import { ref, computed, watch } from 'vue';
import { normalizeOptions, isNumberType } from './configUtils.js';
import FileBrowser from '../common/FileBrowser.vue';
import IconFolderOpen from '~icons/tabler/folder-open';

const props = defineProps({
  entry: { type: Object, required: true },
});
const emit = defineEmits(['commit']);

const local = ref(props.entry.value != null ? String(props.entry.value) : '');
watch(() => props.entry.value, (v) => { local.value = v != null ? String(v) : ''; });

const options = computed(() => normalizeOptions(props.entry.accepts));
const numeric = computed(() => isNumberType(props.entry.type));
const isBool = computed(() => (props.entry.type || '').toLowerCase() === 'boolean');

// 启发式：约定后缀 Track 结尾的字符串字段是"曲目来源"（url 或本地路径），
// 才显示"选择文件"按钮。后端目前没有把
// "这是路径"作为协议的一部分显式声明，这是前端按命名约定做的推断，
// 不是通用协议——未来如果后端加上专门的 accepts.isPath 标记，这里改用那个更准。
const looksLikePathField = computed(() =>
  !isBool.value && !options.value && (props.entry.type || '').toLowerCase() === 'string'
  && /Track$/.test(props.entry.key || ''));

// 文件夹按钮 → WebUI 内置文件浏览器（原生对话框在游戏 Mono 里加载不了
// System.Windows.Forms，改为浏览器内直接浏览本机目录；文本框手输路径始终可用）
const browsing = ref(false);
function onPicked(p) {
  browsing.value = false;
  local.value = p;
  commit();
}

function commit() {
  if (isBool.value) return;   // 布尔走独立开关，不经过这个 commit
  let v = local.value;
  if (numeric.value && v !== '') {
    const n = Number(v);
    const min = props.entry.accepts && typeof props.entry.accepts.min === 'number' ? props.entry.accepts.min : null;
    const max = props.entry.accepts && typeof props.entry.accepts.max === 'number' ? props.entry.accepts.max : null;
    if ((min != null && n < min) || (max != null && n > max)) {
      alert(`超出范围 ${min ?? '-∞'} ~ ${max ?? '+∞'}`);
      local.value = props.entry.value != null ? String(props.entry.value) : '';
      return;
    }
  }
  emit('commit', v);
}

function toggleBool() {
  emit('commit', !props.entry.value);
}
</script>

<template>
  <label v-if="isBool" class="switch">
    <input type="checkbox" :checked="!!entry.value" @change="toggleBool">
    <span class="switch-track"><span class="switch-thumb" /></span>
  </label>

  <select v-else-if="options" class="cfg-select" :value="entry.value" @change="emit('commit', $event.target.value)">
    <option v-for="opt in options" :key="opt.value" :value="opt.value">{{ opt.label || opt.value }}</option>
  </select>

  <div v-else-if="looksLikePathField" class="path-field">
    <input
      v-model="local"
      class="cfg-input"
      type="text"
      placeholder="http(s):// 链接，或本地文件绝对路径"
      @change="commit"
      @keydown.enter="commit"
    >
    <button class="pick-btn" type="button" title="在 WebUI 中浏览本机文件" @click="browsing = true">
      <IconFolderOpen />
    </button>
    <FileBrowser
      v-if="browsing"
      :start-path="local"
      title="选择音频文件"
      @select="onPicked"
      @close="browsing = false"
    />
  </div>

  <input
    v-else
    v-model="local"
    class="cfg-input"
    :type="numeric ? 'number' : 'text'"
    :min="entry.accepts && entry.accepts.min"
    :max="entry.accepts && entry.accepts.max"
    @change="commit"
    @keydown.enter="commit"
  >
</template>

<style scoped>
.switch { display: inline-flex; cursor: pointer; }
.switch input { position: absolute; opacity: 0; width: 0; height: 0; }
.switch-track {
  width: 38px; height: 22px;
  background: var(--surface-2);
  border: 1px solid var(--line-strong);
  border-radius: 12px;
  position: relative;
  transition: background 0.15s var(--ease), border-color 0.15s var(--ease);
}
.switch-thumb {
  position: absolute;
  top: 2px; left: 2px;
  width: 16px; height: 16px;
  border-radius: 50%;
  background: var(--text-2);
  transition: transform 0.15s var(--ease), background 0.15s var(--ease);
}
.switch input:checked + .switch-track { background: rgba(0, 229, 255, 0.18); border-color: var(--accent-cyan-dim); }
.switch input:checked + .switch-track .switch-thumb { transform: translateX(16px); background: var(--accent-cyan); }

.cfg-select, .cfg-input {
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12.5px;
  padding: 6px 9px;
  outline: none;
  font-family: var(--font-mono);
  min-width: 0;
}
.cfg-select:focus, .cfg-input:focus { border-color: var(--accent-cyan-dim); }

.path-field { display: flex; gap: 6px; flex: 1; min-width: 0; }
.path-field .cfg-input { flex: 1; min-width: 0; }
.pick-btn {
  flex-shrink: 0;
  width: 32px;
  display: grid;
  place-items: center;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.pick-btn:hover { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.pick-btn svg { width: 15px; height: 15px; }
</style>
