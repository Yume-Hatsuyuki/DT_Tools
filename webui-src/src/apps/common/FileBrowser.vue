<script setup>
import { ref, onMounted, onUnmounted } from 'vue';
import { API } from '../../api.js';
import IconFolder from '~icons/tabler/folder';
import IconMusic from '~icons/tabler/music';
import IconFile from '~icons/tabler/file';
import IconArrowUp from '~icons/tabler/arrow-up';
import IconX from '~icons/tabler/x';

/**
 * 本机文件选择弹窗（WebUI 内置浏览器）——游戏 Mono 加载不了 System.Windows.Forms，
 * 原生对话框方案已整体移除，选择文件一律走这里：数据源 GET /api/fs/list
 * （只读目录列举，本机后端代为浏览）。
 * 必须整块 Teleport 到 body：窗口外壳 .win 带 backdrop-filter，会成为 fixed 后代的
 * 包含块，留在窗口内遮罩只能盖住所在窗口（同 FolderGrid 菜单的教训）。
 */
const props = defineProps({
  /** 初始位置：本地文件路径→落到所在目录；本地目录路径→进入该目录；空/在线链接→盘符根视图。 */
  startPath: { type: String, default: '' },
  title: { type: String, default: '选择文件' },
});
const emit = defineEmits(['select', 'close']);

const cur = ref('');        // 当前目录（""=盘符根视图）
const parent = ref(null);
const entries = ref([]);
const truncated = ref(false);
const sel = ref(null);      // 选中的文件完整路径
const addr = ref('');       // 地址栏
const loading = ref(false);
const err = ref('');

async function go(p) {
  loading.value = true;
  err.value = '';
  const r = await API.fsList(p);
  loading.value = false;
  if (r.unauthorized) return;
  if (!r.ok) {
    err.value = r.message || r.error || '列举失败';
    return;
  }
  cur.value = r.path;
  parent.value = r.parent;
  entries.value = r.entries || [];
  truncated.value = !!r.truncated;
  addr.value = r.path;
  sel.value = null;
}

onMounted(() => {
  const sp = (props.startPath || '').trim();
  const isLocal = sp && !/^https?:\/\//i.test(sp);
  go(isLocal ? sp : '');
  window.addEventListener('keydown', onKey);
});
onUnmounted(() => window.removeEventListener('keydown', onKey));

function onKey(e) {
  if (e.key === 'Escape') emit('close');
}

function pick(entry) {
  if (entry.isDir) {
    go(entry.path);
    return;
  }
  sel.value = entry.path;
}
function confirmSel() {
  if (sel.value) emit('select', sel.value);
}

function fmtSize(bytes) {
  if (!bytes) return '';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  if (bytes < 1024 * 1024 * 1024) return (bytes / 1024 / 1024).toFixed(1) + ' MB';
  return (bytes / 1024 / 1024 / 1024).toFixed(2) + ' GB';
}
</script>

<template>
  <Teleport to="body">
    <div class="fb-mask" @contextmenu.prevent>
      <div class="fb-box">
        <div class="fb-head">
          <span>{{ title }}</span>
          <button class="fb-close" type="button" @click="emit('close')"><IconX /></button>
        </div>

        <div class="fb-nav">
          <button
            class="fb-up"
            type="button"
            :disabled="parent == null"
            title="返回上一级"
            @click="go(parent)"
          >
            <IconArrowUp />
          </button>
          <input
            v-model="addr"
            class="fb-addr"
            type="text"
            spellcheck="false"
            placeholder="输入目录或文件路径后回车"
            @keydown.enter="go(addr)"
          >
        </div>

        <div class="fb-list">
          <div v-if="loading" class="fb-state">加载中…</div>
          <template v-else-if="err">
            <div class="fb-state fb-err">{{ err }}</div>
            <div class="fb-state-actions">
              <button type="button" @click="go('')">返回盘符列表</button>
            </div>
          </template>
          <template v-else>
            <button
              v-for="e in entries"
              :key="e.path"
              class="fb-row"
              type="button"
              :class="{ dir: e.isDir, audio: !e.isDir && e.audio, selected: sel === e.path }"
              :title="e.path"
              @click="pick(e)"
              @dblclick="!e.isDir && confirmSel()"
            >
              <IconFolder v-if="e.isDir" class="fb-ico dir" />
              <IconMusic v-else-if="e.audio" class="fb-ico audio" />
              <IconFile v-else class="fb-ico other" />
              <span class="fb-name">{{ e.name }}</span>
              <span class="fb-size">{{ e.isDir ? '' : fmtSize(e.size) }}</span>
            </button>
            <div v-if="!entries.length" class="fb-state">此目录为空。</div>
            <div v-if="truncated" class="fb-state fb-truncated">条目过多，仅显示前 {{ entries.length }} 项（可用地址栏直达子目录）。</div>
          </template>
        </div>

        <div class="fb-foot">
          <span class="fb-sel" :title="sel || ''">{{ sel || '未选择文件' }}</span>
          <div class="fb-actions">
            <button type="button" @click="emit('close')">取消</button>
            <button type="button" class="primary" :disabled="!sel" @click="confirmSel">使用所选</button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.fb-mask {
  position: fixed;
  inset: 0;
  z-index: 10001;
  background: rgba(0, 0, 0, 0.55);
  display: grid;
  place-items: center;
}
.fb-box {
  width: 560px;
  max-width: 92vw;
  height: 440px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--win-shadow);
  overflow: hidden;
}

.fb-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  font-size: 13px;
  color: var(--text-0);
  border-bottom: 1px solid var(--line);
}
.fb-close {
  background: transparent;
  border: none;
  color: var(--text-2);
  cursor: pointer;
  display: grid;
  place-items: center;
  padding: 2px;
}
.fb-close:hover { color: var(--text-0); }
.fb-close svg { width: 15px; height: 15px; }

.fb-nav {
  display: flex;
  gap: 6px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--line);
}
.fb-up {
  flex-shrink: 0;
  width: 30px;
  display: grid;
  place-items: center;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
}
.fb-up:hover:not(:disabled) { color: var(--accent-cyan); border-color: var(--accent-cyan-dim); }
.fb-up:disabled { opacity: 0.4; cursor: default; }
.fb-up svg { width: 14px; height: 14px; }
.fb-addr {
  flex: 1;
  min-width: 0;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 12px;
  padding: 6px 9px;
  outline: none;
  font-family: var(--font-mono);
}
.fb-addr:focus { border-color: var(--accent-cyan-dim); }

.fb-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px 6px;
}
.fb-row {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 8px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  color: var(--text-1);
  cursor: pointer;
  text-align: left;
}
.fb-row:hover { background: var(--surface-1); border-color: var(--line); }
.fb-row.selected { background: rgba(0, 229, 255, 0.10); border-color: var(--accent-cyan-dim); }
.fb-row.dir { color: var(--text-0); }
.fb-ico { flex-shrink: 0; width: 15px; height: 15px; }
.fb-ico.dir { color: var(--accent-cyan); }
.fb-ico.audio { color: var(--accent-green); }
.fb-ico.other { color: var(--text-2); opacity: 0.7; }
.fb-name {
  flex: 1;
  min-width: 0;
  font-family: var(--font-mono);
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.fb-row.audio .fb-name { color: var(--text-0); }
.fb-size { flex-shrink: 0; font-size: 10.5px; color: var(--text-2); font-family: var(--font-mono); }

.fb-state { text-align: center; color: var(--text-2); padding: 32px 0; font-size: 12.5px; }
.fb-err { color: var(--accent-red); }
.fb-state-actions { text-align: center; }
.fb-state-actions button {
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 6px 14px;
  cursor: pointer;
}
.fb-state-actions button:hover { color: var(--text-0); }
.fb-truncated { padding: 8px 0; font-size: 11px; }

.fb-foot {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border-top: 1px solid var(--line);
}
.fb-sel {
  flex: 1;
  min-width: 0;
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--text-2);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.fb-actions { display: flex; gap: 8px; flex-shrink: 0; }
.fb-actions button {
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 6px 14px;
  cursor: pointer;
}
.fb-actions button:hover:not(:disabled) { color: var(--text-0); }
.fb-actions button:disabled { opacity: 0.45; cursor: default; }
.fb-actions button.primary {
  background: var(--accent-blue-btn);
  border-color: transparent;
  color: #fff;
}
.fb-actions button.primary:hover:not(:disabled) { filter: brightness(1.1); color: #fff; }
</style>
