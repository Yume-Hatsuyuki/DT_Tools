import { reactive } from 'vue';

/**
 * 全局连接状态：由控制台的日志流（SSE/轮询）驱动，任务栏读取展示。
 * 用简单的模块级单例而非 provide/inject——全桌面只有一份连接状态，
 * 多处需要读，用 store 式单例比逐层传值简单。
 */
const state = reactive({ online: false });

export function useConnectionStatus() {
  return state;
}
