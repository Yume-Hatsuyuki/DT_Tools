import { ref } from 'vue';

/**
 * 文件夹视图模式（网格 / 列表）—— **DT 配置页与自动化页共用一份**。
 *
 * 为什么做成模块级单例：两页用的是同一个 FolderGrid 组件。若各页自持状态，
 * 用户在配置页切成列表、切到自动化页又变回网格，很割裂；共用一份则任一处切换、
 * 另一处立刻跟着变。
 *
 * 与其它壳层本地状态一样只进 localStorage（不进后端配置）。
 */
const KEY = 'dt_foldergrid_view_v1';

export const folderViewMode = ref(localStorage.getItem(KEY) === 'list' ? 'list' : 'grid');

export function toggleFolderView() {
  folderViewMode.value = folderViewMode.value === 'list' ? 'grid' : 'list';
  localStorage.setItem(KEY, folderViewMode.value);
}

export function useFolderView() {
  return { viewMode: folderViewMode, toggleView: toggleFolderView };
}
