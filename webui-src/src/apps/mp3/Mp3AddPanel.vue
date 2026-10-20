<script setup>
import { ref, computed, onMounted } from 'vue';
import { API } from '../../api.js';

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
const settings = ref({
  ipMode: 'ipinfo',
  fallbackIp: '1.1.1.1',
  level: 'standard',
  userAgent: '',
  levels: ['standard', 'exhigh', 'lossless'],
  defaultFallbackIp: '1.1.1.1',
  defaultUserAgent: '',
});
const settingsLoaded = ref(false);

async function loadSettings() {
  const j = await API.mp3NeteaseSettings();
  if (j.ok) {
    settings.value = { ...settings.value, ...j.data };
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
  <div class="mp3-add-mask" @click.self="emit('close')">
    <div class="mp3-add" role="dialog" aria-label="添加音乐">
      <header class="mp3-add-head">
        <div class="mp3-add-tabs" role="tablist">
          <button role="tab" :class="{ on: tab === 'local' }" :aria-selected="tab === 'local'" @click="tab = 'local'">本地音乐</button>
          <button role="tab" :class="{ on: tab === 'https' }" :aria-selected="tab === 'https'" @click="tab = 'https'">链接（http/https）</button>
          <button role="tab" :class="{ on: tab === 'netease' }" :aria-selected="tab === 'netease'" @click="tab = 'netease'">网易云音乐</button>
        </div>
        <button class="icon-btn" title="关闭" @click="emit('close')">×</button>
      </header>

      <section v-if="tab === 'local'" class="mp3-add-body">
        <p class="hint">浏览本机文件，选中后加入当前歌单。文件名保持不变，可在列表里单独设置标签。</p>
        <button class="tb-btn" :disabled="busy" @click="emit('pick-local')">选择本机文件…</button>
      </section>

      <section v-else-if="tab === 'https'" class="mp3-add-body">
        <label>链接<input v-model="httpUrl" placeholder="https://… 或 http://…（例如 NAS 上的地址）" /></label>
        <label>名字（必填）<input v-model="httpName" maxlength="60" placeholder="给这首歌起个名字" /></label>
        <button class="tb-btn" :disabled="!httpReady || busy" @click="addHttp">添加</button>
      </section>

      <section v-else class="mp3-add-body">
        <label>歌曲链接或 ID<input v-model="neteaseInput" placeholder="https://music.163.com/#/song?id=… 或纯数字 ID" /></label>
        <div class="mp3-add-row">
          <label class="grow">名字（可留空，留空则显示「网易云 ID」）<input v-model="neteaseName" maxlength="60" /></label>
          <button class="tb-btn" :disabled="!neteaseReady || busy" title="向接口查询歌名并填入名字" @click="fetchNeteaseName">获取名字</button>
        </div>
        <button class="tb-btn" :disabled="!neteaseReady || busy" @click="addNetease">添加</button>

        <details class="mp3-netease-settings">
          <summary>解析设置</summary>
          <fieldset>
            <legend>IP 来源</legend>
            <label><input type="radio" value="ipinfo" v-model="settings.ipMode" /> ipinfo（默认，自动获取公网 IP）</label>
            <label><input type="radio" value="custom" v-model="settings.ipMode" /> 自定义 IP（不请求 ipinfo.io，适合 ipinfo 限流时）</label>
            <label>IP 地址（自定义模式使用；ipinfo 模式下作为失败回退）<input v-model="settings.fallbackIp" :placeholder="settings.defaultFallbackIp" /></label>
          </fieldset>
          <label>音质
            <select v-model="settings.level">
              <option v-for="l in settings.levels" :key="l" :value="l">{{ l }}</option>
            </select>
          </label>
          <label>User-Agent（留空使用默认）<input v-model="settings.userAgent" :placeholder="settings.defaultUserAgent" /></label>
          <button class="tb-btn" :disabled="!settingsLoaded || busy" @click="saveSettings">保存解析设置</button>
        </details>
      </section>
    </div>
  </div>
</template>

<style scoped>
.mp3-add-mask { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.45); display: flex; align-items: center; justify-content: center; z-index: 60; }
.mp3-add { width: min(460px, 92vw); max-height: 86vh; overflow: auto; background: var(--surface, #1b1f2a); color: var(--text-1, #e6e9f0); border-radius: 10px; padding: 12px 14px; }
.mp3-add-head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 10px; }
.mp3-add-tabs { display: flex; gap: 6px; flex-wrap: wrap; }
.mp3-add-tabs button { padding: 4px 10px; border-radius: 6px; border: 1px solid transparent; background: transparent; color: inherit; cursor: pointer; }
.mp3-add-tabs button.on { border-color: var(--accent-cyan, #4fd1c5); }
.mp3-add-body { display: flex; flex-direction: column; gap: 10px; }
.mp3-add-body label { display: flex; flex-direction: column; gap: 4px; font-size: 12.5px; }
.mp3-add-body input, .mp3-add-body select { padding: 6px 8px; border-radius: 6px; border: 1px solid rgba(255, 255, 255, 0.15); background: rgba(0, 0, 0, 0.2); color: inherit; }
.mp3-add-row { display: flex; gap: 8px; align-items: flex-end; }
.mp3-add-row .grow { flex: 1; min-width: 0; }
.mp3-netease-settings fieldset { border: 1px solid rgba(255, 255, 255, 0.12); border-radius: 6px; padding: 8px; display: flex; flex-direction: column; gap: 6px; }
.hint { font-size: 12px; opacity: 0.75; margin: 0; }
</style>
