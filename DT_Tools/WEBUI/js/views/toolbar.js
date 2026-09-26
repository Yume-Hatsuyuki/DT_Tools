/**
 * CONFIG / AUTOMATION 共用面板工具栏：保存 / 导出 / 导入 / 全部重置 / 自动刷新 / 未保存标记。
 * 两个视图只传元素与 reload 回调，业务逻辑只写这一份。
 * 未保存标记集中记录在 dirtyState，由 main.js 统一做离开提醒。
 */

import { API, createPoller } from '../api.js';
import { toast } from '../ui.js';

/** 各面板的未保存标记（key 由 opt.flag 指定）。 */
export const dirtyState = {};

export function wireToolbar(opt) {
  const setDirty = (v) => {
    if (opt.flag) dirtyState[opt.flag] = !!v;
    if (opt.dirtyEl) opt.dirtyEl.style.display = v ? 'inline' : 'none';
  };

  // 保存到 .cfg
  opt.saveBtn.onclick = async () => {
    const j = await API.configSave();
    if (j.ok) { setDirty(false); toast('已保存到 .cfg'); }
    else toast(j.error || '保存失败', true);
  };

  // 导出 .cfg（同源下载）
  opt.exportBtn.onclick = () => {
    const a = document.createElement('a');
    a.href = API.exportCfgUrl;
    a.download = 'DT_Tools.cfg';
    a.click();
  };

  // 导入（memory=仅内存生效，overwrite=写盘）
  const importFile = (mode) => {
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
      if (!j.ok) { toast(j.error || '导入失败', true); return; }
      setDirty(mode === 'memory');
      const errs = (j.errors && j.errors.length) ? ' errors=' + j.errors.length : '';
      toast('导入完成：updated=' + j.updated + ' skipped=' + j.skipped + errs);
      await opt.reload();
    };
    input.click();
  };
  opt.importMemBtn.onclick = () => importFile('memory');
  opt.importOverBtn.onclick = () => importFile('overwrite');

  // 全部恢复默认
  opt.resetAllBtn.onclick = async () => {
    if (!confirm('恢复全部配置默认值？（仅做临时调整，如需持久化请使用保存功能。）')) return;
    const j = await API.configReset();
    if (j.ok) { setDirty(true); toast('已重置 ' + j.reset + ' 项'); await opt.reload(); }
    else toast(j.error || '重置失败', true);
  };

  // 自动刷新：面板激活时每 3s 重拉；编辑控件中跳过；标签页隐藏时由 poller 暂停
  let active = false;
  let autoRefresh = true;
  const syncBtn = () => {
    opt.autoBtn.textContent = autoRefresh ? '自动刷新:开' : '自动刷新:关';
    opt.autoBtn.title = autoRefresh ? '点击关闭面板自动刷新' : '点击开启面板自动刷新';
  };
  syncBtn();

  const poll = createPoller(async () => {
    if (!active || !autoRefresh) return;
    if (opt.isEditingInPanel && opt.isEditingInPanel()) return;
    await opt.reload();
  }, 3000);

  opt.autoBtn.onclick = () => {
    autoRefresh = !autoRefresh;
    syncBtn();
    if (autoRefresh && active) opt.reload();
  };

  return {
    setDirty,
    /** 面板激活/停用（由路由调用）。 */
    setActive(on) {
      active = !!on;
      if (active) poll.start();
      else poll.stop();
    },
  };
}
