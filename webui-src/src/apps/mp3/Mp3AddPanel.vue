<script setup>
import { ref, computed, onMounted } from 'vue';
import { API } from '../../api.js';
import IconX from '~icons/tabler/x';
import IconFolder from '~icons/tabler/folder';
import IconLink from '~icons/tabler/link';
import IconMusic from '~icons/tabler/music';

const props = defineProps({
  listId: { type: String, default: null },
});
const emit = defineEmits(['close', 'pick-local', 'added', 'error']);

const tab = ref('local');
const busy = ref(false);

const httpUrl = ref('');
const httpName = ref('');
const httpReady = computed(() => /^https?:\/\//i.test(httpUrl.value.trim()) && httpName.value.trim().length > 0);

const neteaseInput = ref('');
const neteaseName = ref('');
const neteaseReady = computed(() => neteaseInput.value.trim().length > 0);

/** 与 tools/nextmusic-dl.mjs / NeteaseMusic.Levels 对齐的兜底列表 */
const FALLBACK_LEVELS = [
  { value: 'standard', label: '标准音质' },
  { value: 'higher', label: '较高音质' },
  { value: 'exhigh', label: '极高音质' },
  { value: 'lossless', label: '无损音质' },
  { value: 'hires', label: 'Hi-Res音质' },
  { value: 'sky', label: '沉浸环绕声' },
  { value: 'jyeffect', label: '高清环绕声' },
  { value: 'jymaster', label: '超清母带' },
  { value: 'dolby', label: '杜比全景声' },
];

const settings = ref({
  ipMode: 'ipinfo',
  fallbackIp: '1.1.1.1',
  level: 'standard',
  userAgent: '',
  levels: FALLBACK_LEVELS.map((x) => x.value),
  levelOptions: FALLBACK_LEVELS,
  defaultFallbackIp: '1.1.1.1',
  defaultUserAgent: '',
});
const settingsLoaded = ref(false);

/** 下拉选项：优先用后端 levelOptions，否则用 levels 码 + 本地中文表 */
const levelSelectOptions = computed(() => {
  const opts = settings.value.levelOptions;
  if (Array.isArray(opts) && opts.length && opts[0]?.value != null) return opts;
  const codes = settings.value.levels || [];
  const map = new Map(FALLBACK_LEVELS.map((x) => [x.value, x.label]));
  return codes.map((v) => ({ value: v, label: map.get(v) || v }));
});

async function loadSettings() {
  const j = await API.mp3NeteaseSettings();
  if (j.ok) {
    const data = j.data || {};
    // 旧后端只有 levels 字符串数组时，仍可显示
    if (!data.levelOptions && Array.isArray(data.levels)) {
      const map = new Map(FALLBACK_LEVELS.map((x) => [x.value, x.label]));
      data.levelOptions = data.levels.map((v) => ({ value: v, label: map.get(v) || v }));
    }
    settings.value = { ...settings.value, ...data };
    settingsLoaded.value = true;
  }
}

onMounted(loadSettings);

async function fetchNeteaseName() {
  if (!neteaseReady.value || busy.value) return;
  busy.value = true;
  try {
    const j = await API.mp3NeteaseInfo(neteaseInput.value.trim(), settings.value.ipMode, settings.value.fallbackIp);
    if (!j.ok) {
      emit('error', j.data?.message || j.error || '获取歌曲信息失败');
      return;
    }
    if (!neteaseName.value.trim()) neteaseName.value = j.data?.name || '';
  } finally {
    busy.value = false;
  }
}

async function saveSettings() {
  const j = await API.mp3NeteaseSettingsSet({
    ipMode: settings.value.ipMode,
    fallbackIp: settings.value.fallbackIp,
    level: settings.value.level,
    userAgent: settings.value.userAgent,
  });
  if (!j.ok) emit('error', j.data?.message || j.error || '保存失败');
  else await loadSettings();
}

async function addAndClose(send) {
  busy.value = true;
  try {
    const j = await send();
    if (j.unauthorized) return;
    if (!j.ok) {
      emit('error', j.data?.message || j.error || '添加失败');
      return;
    }
    emit('added', j.data?.index ?? -1);
  } finally {
    busy.value = false;
  }
}

function addHttp() {
  if (!httpReady.value || busy.value) return;
  return addAndClose(() => API.mp3PlaylistAdd(httpUrl.value.trim(), props.listId, { kind: 'https', name: httpName.value.trim() }));
}

function addNetease() {
  if (!neteaseReady.value || busy.value) return;
  return addAndClose(() => API.mp3PlaylistAdd(neteaseInput.value.trim(), props.listId, { kind: 'netease', name: neteaseName.value.trim() }));
}
</script>

<template>
  <div class="mask" @click.self="emit('close')">
    <div class="dialog" role="dialog" aria-modal="true" aria-label="添加音乐">
      <header class="dhead">
        <span class="dtitle">添加音乐</span>
        <button type="button" class="dclose" title="关闭" @click="emit('close')"><IconX /></button>
      </header>

      <div class="tabs" role="tablist">
        <button
          type="button"
          role="tab"
          class="tab"
          :class="{ on: tab === 'local' }"
          :aria-selected="tab === 'local'"
          @click="tab = 'local'"
        >
          <IconFolder /> 本地音乐
        </button>
        <button
          type="button"
          role="tab"
          class="tab"
          :class="{ on: tab === 'https' }"
          :aria-selected="tab === 'https'"
          @click="tab = 'https'"
        >
          <IconLink /> 链接
        </button>
        <button
          type="button"
          role="tab"
          class="tab"
          :class="{ on: tab === 'netease' }"
          :aria-selected="tab === 'netease'"
          @click="tab = 'netease'"
        >
          <IconMusic /> 网易云
        </button>
      </div>

      <section v-if="tab === 'local'" class="body">
        <p class="hint">浏览本机文件，选中后加入当前歌单。文件名保持不变，可在列表里单独设置标签。</p>
        <div class="actions">
          <button type="button" class="tb-btn primary" :disabled="busy" @click="emit('pick-local')">
            <IconFolder /> 选择本机文件…
          </button>
        </div>
      </section>

      <section v-else-if="tab === 'https'" class="body">
        <label class="field">
          <span class="field-label">链接</span>
          <input v-model="httpUrl" spellcheck="false" placeholder="https://… 或 http://…（例如 NAS 上的地址）" />
        </label>
        <label class="field">
          <span class="field-label">名字（必填）</span>
          <input v-model="httpName" maxlength="60" placeholder="给这首歌起个名字" />
        </label>
        <div class="actions">
          <button type="button" class="tb-btn" @click="emit('close')">取消</button>
          <button type="button" class="tb-btn primary" :disabled="!httpReady || busy" @click="addHttp">添加</button>
        </div>
      </section>

      <section v-else class="body">
        <label class="field">
          <span class="field-label">歌曲链接或 ID</span>
          <input v-model="neteaseInput" spellcheck="false" placeholder="https://music.163.com/#/song?id=… 或纯数字 ID" />
        </label>
        <div class="row">
          <label class="field grow">
            <span class="field-label">名字（可留空，留空则显示「网易云 ID」）</span>
            <input v-model="neteaseName" maxlength="60" />
          </label>
          <button type="button" class="tb-btn" :disabled="!neteaseReady || busy" title="向接口查询歌名并填入名字" @click="fetchNeteaseName">
            获取名字
          </button>
        </div>
        <div class="actions">
          <button type="button" class="tb-btn" @click="emit('close')">取消</button>
          <button type="button" class="tb-btn primary" :disabled="!neteaseReady || busy" @click="addNetease">添加</button>
        </div>

        <details class="settings">
          <summary>解析设置</summary>
          <fieldset>
            <legend>IP 来源</legend>
            <label class="radio">
              <input type="radio" value="ipinfo" v-model="settings.ipMode" />
              <span>ipinfo（默认，自动获取公网 IP）</span>
            </label>
            <label class="radio">
              <input type="radio" value="custom" v-model="settings.ipMode" />
              <span>自定义 IP（不请求 ipinfo.io，适合 ipinfo 限流时）</span>
            </label>
            <label class="field">
              <span class="field-label">IP 地址（自定义模式使用；ipinfo 模式下作为失败回退）</span>
              <input v-model="settings.fallbackIp" :placeholder="settings.defaultFallbackIp" />
            </label>
          </fieldset>
          <label class="field">
            <span class="field-label">音质</span>
            <select v-model="settings.level">
              <option v-for="opt in levelSelectOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
          </label>
          <label class="field">
            <span class="field-label">User-Agent（留空使用默认）</span>
            <input v-model="settings.userAgent" :placeholder="settings.defaultUserAgent" />
          </label>
          <div class="actions">
            <button type="button" class="tb-btn primary" :disabled="!settingsLoaded || busy" @click="saveSettings">
              保存解析设置
            </button>
          </div>
        </details>
      </section>
    </div>
  </div>
</template>

<style scoped>
/* 与平板导出框 / FileBrowser / 语言弹窗统一：深色毛玻璃面板 + 设计令牌 */
.mask {
  position: absolute;
  inset: 0;
  z-index: 40;
  background: rgba(4, 8, 14, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  box-sizing: border-box;
}

.dialog {
  width: min(480px, 100%);
  max-height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--surface-1);
  border: 1px solid var(--line-strong);
  border-radius: var(--radius-md);
  box-shadow: var(--menu-shadow);
  overflow: hidden;
  color: var(--text-1);
}

.dhead {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 14px;
  border-bottom: 1px solid var(--line);
  flex-shrink: 0;
}
.dtitle {
  font-size: 13.5px;
  font-weight: 600;
  color: var(--text-0);
}
.dclose {
  width: 26px;
  height: 26px;
  display: grid;
  place-items: center;
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-2);
  cursor: pointer;
}
.dclose:hover {
  background: rgba(140, 180, 255, 0.12);
  color: var(--text-0);
}
.dclose svg {
  width: 14px;
  height: 14px;
}

.tabs {
  display: flex;
  gap: 6px;
  padding: 10px 14px 0;
  flex-shrink: 0;
}
.tab {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  flex: 1;
  justify-content: center;
  background: var(--surface-0);
  border: 1px solid var(--line);
  border-radius: 999px;
  color: var(--text-2);
  font-size: 12px;
  padding: 6px 10px;
  cursor: pointer;
  white-space: nowrap;
}
.tab svg {
  width: 13px;
  height: 13px;
  flex: none;
}
.tab:hover {
  color: var(--text-0);
  border-color: var(--line-strong);
}
.tab.on {
  color: var(--accent-cyan);
  border-color: var(--accent-cyan-dim);
  background: var(--surface-2);
}

.body {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 14px;
  overflow-y: auto;
  flex: 1;
  min-height: 0;
}

.hint {
  margin: 0;
  font-size: 12px;
  line-height: 1.5;
  color: var(--text-2);
}

.field {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}
.field-label {
  font-size: 11.5px;
  color: var(--text-2);
}
.field input,
.field select {
  width: 100%;
  padding: 7px 10px;
  border-radius: var(--radius-sm);
  border: 1px solid var(--line);
  background: var(--surface-0);
  color: var(--text-0);
  font-size: 12.5px;
  outline: none;
}
.field input:focus,
.field select:focus {
  border-color: var(--accent-cyan-dim);
}
.field select {
  cursor: pointer;
}

.row {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}
.row .grow {
  flex: 1;
  min-width: 0;
}
.row .tb-btn {
  flex-shrink: 0;
  margin-bottom: 0;
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-top: 4px;
}

.tb-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  background: var(--surface-2);
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--text-1);
  font-size: 11.5px;
  padding: 6px 12px;
  cursor: pointer;
  white-space: nowrap;
}
.tb-btn svg {
  width: 13px;
  height: 13px;
}
.tb-btn:hover:not(:disabled) {
  color: var(--text-0);
  border-color: var(--line-strong);
}
.tb-btn:disabled {
  opacity: 0.45;
  cursor: default;
}
.tb-btn.primary {
  background: var(--accent-blue-btn);
  border-color: transparent;
  color: #fff;
}
.tb-btn.primary:hover:not(:disabled) {
  filter: brightness(1.08);
  color: #fff;
}

.settings {
  margin-top: 4px;
  border-top: 1px solid var(--line);
  padding-top: 10px;
}
.settings summary {
  font-size: 12px;
  color: var(--text-2);
  cursor: pointer;
  user-select: none;
  list-style: none;
  display: flex;
  align-items: center;
  gap: 6px;
}
.settings summary::-webkit-details-marker {
  display: none;
}
.settings summary::before {
  content: '▸';
  font-size: 10px;
  color: var(--text-2);
  transition: transform 0.15s var(--ease);
}
.settings[open] summary::before {
  transform: rotate(90deg);
}
.settings[open] summary {
  color: var(--text-1);
  margin-bottom: 10px;
}

.settings fieldset {
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  padding: 10px 12px;
  margin: 0 0 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.settings legend {
  font-size: 11.5px;
  color: var(--text-2);
  padding: 0 4px;
}

.radio {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  font-size: 12px;
  color: var(--text-1);
  cursor: pointer;
}
.radio input {
  margin-top: 2px;
  accent-color: var(--accent-cyan);
  flex: none;
}
</style>
