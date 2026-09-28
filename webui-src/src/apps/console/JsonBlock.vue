<script setup>
import { ref, computed } from 'vue';

const props = defineProps({ text: { type: String, required: true } });
const copied = ref(false);

/** JSON 语法上色：key/string/number/bool-null 五类 span，通过安全拼 innerHTML
 * 的方式渲染——文本内容已由 JSON.stringify 产出、不含用户可控的 HTML 结构，
 * 唯一风险是字符串值本身，这里逐段转义后再包 span，杜绝注入。
 */
function esc(s) {
  return s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

const highlighted = computed(() => {
  const re = /("(?:\\.|[^"\\])*")(\s*:)?|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|\b(true|false|null)\b/g;
  let last = 0, m, out = '';
  const emit = (s, cls) => {
    if (!s) return;
    out += cls ? `<span class="${cls}">${esc(s)}</span>` : esc(s);
  };
  while ((m = re.exec(props.text)) !== null) {
    emit(props.text.slice(last, m.index));
    if (m[1] !== undefined) {
      emit(m[1], m[2] ? 'j-key' : 'j-str');
      if (m[2]) emit(m[2]);
    } else if (m[3] !== undefined) {
      emit(m[3], 'j-num');
    } else {
      emit(m[4], 'j-bool');
    }
    last = re.lastIndex;
  }
  emit(props.text.slice(last));
  return out;
});

async function copy() {
  try {
    await navigator.clipboard.writeText(props.text);
    copied.value = true;
    setTimeout(() => { copied.value = false; }, 1500);
  } catch { /* 剪贴板被拒绝：静默失败，按钮文案不变 */ }
}
</script>

<template>
  <details class="json-fold">
    <summary>json 响应（{{ text.length }} 字符，点击展开）</summary>
    <div class="json-copy-bar">
      <button type="button" class="btn-xs" @click="copy">{{ copied ? '已复制' : '复制 JSON' }}</button>
    </div>
    <pre class="json-body" v-html="highlighted" />
  </details>
</template>

<style scoped>
.json-fold { margin-top: 4px; }
.json-fold summary {
  cursor: pointer;
  font-size: 11.5px;
  color: var(--text-2);
  user-select: none;
}
.json-copy-bar { margin: 6px 0; }
.btn-xs {
  font-size: 11px;
  padding: 3px 9px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.btn-xs:hover { color: var(--text-0); border-color: var(--line-strong); }
.json-body {
  font-family: var(--font-mono);
  font-size: 12px;
  line-height: 1.5;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  padding: 10px 12px;
  overflow-x: auto;
  user-select: text;
  white-space: pre;
}
.json-body :deep(.j-key) { color: var(--accent-cyan); }
.json-body :deep(.j-str) { color: var(--accent-green); }
.json-body :deep(.j-num) { color: var(--accent-amber); }
.json-body :deep(.j-bool) { color: var(--accent-magenta); }
</style>
