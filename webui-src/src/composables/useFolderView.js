import { ref } from 'vue';

const KEY = 'dt_foldergrid_view_v1';

export const folderViewMode = ref(localStorage.getItem(KEY) === 'list' ? 'list' : 'grid');

export function toggleFolderView() {
  folderViewMode.value = folderViewMode.value === 'list' ? 'grid' : 'list';
  localStorage.setItem(KEY, folderViewMode.value);
}

export function useFolderView() {
  return { viewMode: folderViewMode, toggleView: toggleFolderView };
}
