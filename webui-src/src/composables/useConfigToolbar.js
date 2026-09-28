import { ref, watch } from 'vue';
import { API, createPoller } from '../api.js';

/**
 * 任一面板（Config/Automation）有未保存改动时，关闭/刷新页面前提醒——
 * 照搬旧版 toolbar.js 的 dirtyState 模块级单例，供 main.js 的 beforeunload 读取。
 */
export const dirtyState = { config: false, automation: false };

/**
 * CONFIG / AUTOMATION 共用面板工具栏逻辑（保存/导出/导入/重置/自动刷新/脏标记），
 * 照搬旧版 toolbar.js 的行为，换成 Vue ref 承载状态。
 * flag: 'config' | 'automation'，用于写回上面的共享 dirtyState。
 */
export function useConfigToolbar({ reload, isEditing, flag } = {}) {
  const dirty = ref(false);
  if (flag) watch(dirty, (v) => { dirtyState[flag] = v; });
  const autoRefresh = ref(true);
  const toast = ref(null);   // { text, error }

  function showToast(text, error = false) {
    toast.value = { text, error };
    setTimeout(() => { if (toast.value && toast.value.text === text) toast.value = null; }, 2200);
  }

  async function save() {
    const j = await API.configSave();
    if (j.unauthorized) return;
    if (j.ok) { dirty.value = false; showToast('已保存到 .cfg'); }
    else showToast(j.error || '保存失败', true);
  }

  function exportCfg() {
    const a = document.createElement('a');
    a.href = API.exportCfgUrl;
    a.download = 'DT_Tools.cfg';
    a.click();
  }

  function importFile(mode) {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.cfg,.json,text/plain,application/json';
    input.onchange = async () => {
      const file = input.files[0];
      if (!file) return;
      if (mode === 'overwrite' && !confirm('覆盖配置将写入磁盘 .cfg，确定？')) return;
      const content = await file.text();
      const format = file.name.toLowerCase().endsWith('.cfg') ? 'cfg' : 'json';
      const j = await API.configImport(format, mode, content);
      if (j.unauthorized) return;
      if (!j.ok) { showToast(j.error || '导入失败', true); return; }
      dirty.value = mode === 'memory';
      const errs = (j.errors && j.errors.length) ? ' errors=' + j.errors.length : '';
      showToast('导入完成：updated=' + j.updated + ' skipped=' + j.skipped + errs);
      await reload();
    };
    input.click();
  }

  async function resetAll() {
    if (!confirm('恢复全部配置默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
    const j = await API.configReset();
    if (j.unauthorized) return;
    if (j.ok) { dirty.value = true; showToast('已重置 ' + j.reset + ' 项'); await reload(); }
    else showToast(j.error || '重置失败', true);
  }

  let poller = null;
  function startAutoRefresh() {
    if (poller) return;
    poller = createPoller(async () => {
      if (!autoRefresh.value) return;
      if (isEditing && isEditing()) return;
      await reload();
    }, 3000);
    poller.start();
  }
  function stopAutoRefresh() { poller && poller.stop(); }

  return { dirty, autoRefresh, toast, showToast, save, exportCfg, importFile, resetAll, startAutoRefresh, stopAutoRefresh };
}
