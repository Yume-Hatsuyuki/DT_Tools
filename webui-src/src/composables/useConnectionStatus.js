import { reactive } from 'vue';

const state = reactive({ online: false });

export function useConnectionStatus() {
  return state;
}
