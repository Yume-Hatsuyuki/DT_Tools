<script setup>
import { computed, ref } from 'vue';
import { useSectionMeta } from '../../composables/useSectionMeta.js';
import { pickImageFile } from '../../composables/pickImage.js';
import { useCropper } from '../../composables/useCropper.js';
import IconFolder from '~icons/tabler/folder';

const props = defineProps({
  /** [{ key, label, count, enabled: bool|null }] —— key 是稳定标识（段名/模块 id）。 */
  items: { type: Array, required: true },
});
const emit = defineEmits(['open']);

const sectionMeta = useSectionMeta();
const cropper = useCropper();

const menu = ref(null);   // { x, y, item }
const renaming = ref(null);   // { key, value }

function labelOf(item) {
  return sectionMeta.displayName(item.key) || item.label;
}

function openMenu(e, item) {
  e.preventDefault();
  // 视口右/下边缘内收，避免菜单被裁掉
  const x = Math.min(e.clientX, window.innerWidth - 190);
  const y = Math.min(e.clientY, window.innerHeight - 190);
  menu.value = { x, y, item };
}
function closeMenu() { menu.value = null; }

function open(item) {
  emit('open', item);
}

function startRename(item) {
  closeMenu();
  renaming.value = { key: item.key, value: sectionMeta.displayName(item.key) || '' };
}
function commitRename() {
  const r = renaming.value;
  if (!r) return;
  sectionMeta.setName(r.key, r.value);
  renaming.value = null;
}

async function importIcon(item) {
  closeMenu();
  const file = await pickImageFile();
  if (!file) return;
  const dataUrl = await cropper.open(file, { title: '裁切文件夹图标', shape: 'square' });
  if (!dataUrl) return;
  const err = sectionMeta.setIcon(item.key, dataUrl);
  if (err) alert(err);
}
function resetIcon(item) {
  closeMenu();
  sectionMeta.clearIcon(item.key);
}
function resetName(item) {
  closeMenu();
  sectionMeta.setName(item.key, '');
}

const menuItems = computed(() => {
  const it = menu.value?.item;
  if (!it) return [];
  const items = [{ label: '打开', action: () => { closeMenu(); open(it); } }];
  items.push({ label: '重命名…', action: () => startRename(it) });
  if (sectionMeta.displayName(it.key)) {
    items.push({ label: '恢复原名', action: () => resetName(it) });
  }
  items.push({ label: '自定义图标…', action: () => importIcon(it) });
  if (sectionMeta.get(it.key)?.icon) {
    items.push({ label: '恢复默认图标', action: () => resetIcon(it) });
  }
  return items;
});

const filtered = computed(() => props.items);   // 过滤由父级完成（父级还要搜字段）
</script>

<template>
  <div class="folder-grid" @click="closeMenu">
    <button
      v-for="item in filtered"
      :key="item.key"
      class="folder-tile"
      type="button"
      :title="item.label"
      @click="open(item)"
      @contextmenu="openMenu($event, item)"
    >
      <span class="tile-icon">
        <img v-if="sectionMeta.get(item.key)?.icon" :src="sectionMeta.get(item.key).icon" alt="" class="tile-img">
        <IconFolder v-else class="tile-fallback" />
        <span
          v-if="item.enabled !== null && item.enabled !== undefined"
          class="enabled-dot"
          :class="{ on: item.enabled }"
          :title="item.enabled ? '已启用' : '已禁用'"
        />
      </span>
      <span class="tile-label">{{ labelOf(item) }}</span>
      <span class="tile-count">{{ item.count }} 项</span>
    </button>
    <div v-if="!filtered.length" class="empty">无匹配项。</div>

    <!-- 菜单/遮罩必须 Teleport 到 body：窗口外壳 .win 带 backdrop-filter，
         会成为 fixed 后代的包含块，留在窗口内会按窗口左上角错位。 -->
    <Teleport to="body">
      <template v-if="menu">
        <div class="menu-mask" @click="closeMenu" @contextmenu.prevent="closeMenu" />
        <div
          class="tile-menu"
          :style="{ left: menu.x + 'px', top: menu.y + 'px' }"
          @click.stop
          @contextmenu.prevent
        >
          <button v-for="mi in menuItems" :key="mi.label" type="button" @click="mi.action()">{{ mi.label }}</button>
        </div>
      </template>

      <div v-if="renaming" class="rename-mask" @click.self="renaming = null" @contextmenu.prevent>
        <div class="rename-box">
          <div class="rename-title">重命名（仅 WebUI 备忘，不影响配置）</div>
          <input
            v-model="renaming.value"
            class="rename-input"
            type="text"
            maxlength="32"
            placeholder="留空恢复原名"
            @keydown.enter="commitRename"
            @keydown.esc="renaming = null"
          >
          <div class="rename-actions">
            <button type="button" @click="renaming = null">取消</button>
            <button type="button" class="primary" @click="commitRename">确定</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.folder-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(112px, 1fr));
  gap: 4px;
  padding: 16px;
  overflow-y: auto;
  flex: 1;
  align-content: flex-start;
}

.folder-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 14px 8px 10px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  cursor: pointer;
}
.folder-tile:hover { background: var(--surface-1); border-color: var(--line); }

.tile-icon {
  position: relative;
  width: 44px; height: 44px;
  display: grid; place-items: center;
  border-radius: 10px;
  background: linear-gradient(160deg, rgba(39, 127, 255, 0.10), rgba(36, 113, 243, 0.05));
  border: 1px solid var(--line);
  overflow: visible;
}
.tile-icon :deep(svg) { width: 22px; height: 22px; color: var(--accent-cyan); }
.tile-fallback { width: 22px; height: 22px; color: var(--accent-cyan); }
.tile-img {
  position: absolute;
  inset: -4px;
  width: calc(100% + 8px);
  height: calc(100% + 8px);
  object-fit: cover;
  border-radius: 12px;
}
.enabled-dot {
  position: absolute;
  right: -3px; bottom: -3px;
  z-index: 1;
  width: 10px; height: 10px;
  border-radius: 50%;
  background: var(--text-2);
  border: 2px solid var(--surface-0);
}
.enabled-dot.on { background: var(--accent-green); }

.tile-label {
  font-family: var(--font-mono);
  font-size: 11.5px;
  color: var(--text-1);
  text-align: center;
  word-break: break-word;
  line-height: 1.3;
  max-width: 100%;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}
.folder-tile:hover .tile-label { color: var(--text-0); }
.tile-count { font-size: 10px; color: var(--text-2); }

.empty { grid-column: 1 / -1; text-align: center; color: var(--text-2); padding: 40px 0; font-size: 12.5px; }

.tile-menu {
  position: fixed;
  z-index: 9999;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-sm);
  box-shadow: var(--win-shadow);
  backdrop-filter: blur(20px) saturate(150%);
  padding: 4px;
  display: flex;
  flex-direction: column;
  min-width: 170px;
}
.tile-menu button {
  background: transparent;
  border: none;
  color: var(--text-1);
  font-size: 12.5px;
  text-align: left;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  cursor: pointer;
}
.tile-menu button:hover { background: var(--surface-2); color: var(--text-0); }

.rename-mask {
  position: fixed;
  inset: 0;
  z-index: 10000;
  background: rgba(0, 0, 0, 0.45);
  display: grid;
  place-items: center;
}
.rename-box {
  width: 320px;
  background: var(--win-bg);
  border: 1px solid var(--win-border-active);
  border-radius: var(--radius-md);
  box-shadow: var(--win-shadow);
  padding: 14px;
}
.rename-title { font-size: 12px; color: var(--text-1); margin-bottom: 10px; }
.rename-input {
  width: 100%;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-0);
  font-size: 13px;
  padding: 8px 10px;
  outline: none;
}
.rename-input:focus { border-color: var(--accent-cyan-dim); }
.rename-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 12px; }
.rename-actions button {
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 12px;
  padding: 6px 14px;
  cursor: pointer;
}
.rename-actions button:hover { color: var(--text-0); }
.rename-actions button.primary {
  background: var(--accent-blue-btn);
  border-color: transparent;
  color: #fff;
}
.rename-actions button.primary:hover { filter: brightness(1.1); color: #fff; }
</style>
