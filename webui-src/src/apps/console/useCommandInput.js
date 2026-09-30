import { ref } from 'vue';
import { API } from '../../api.js';

/**
 * 控制台输入共享件（两套控制台复用）：命令表加载、历史记录（↑↓ 翻找）、
 * 会话化命令执行。补全候选的匹配与展示形态两套控制台刻意不同
 * （旧版单 token 弹层 + 发送按钮 / 新版 Tab 循环补全），留在各自组件内。
 *
 * 历史按调用方隔离：每次 useCommandInput(appKey) 都得到一份独立的历史与
 * 翻找游标——每个控制台窗口只翻自己输入过的命令，互不"串行"（历史一旦
 * 做成模块级单例，新旧控制台会共用同一份 ↑↓ 序列）。持久化按应用分键
 * （dt_history_<appKey>），同应用新开的窗口沿用该应用的既往历史。
 */

const HISTORY_MAX = 100;

function historyKey(appKey) { return 'dt_history_' + appKey; }

function loadHistory(appKey) {
  try {
    const raw = localStorage.getItem(historyKey(appKey));
    const hist = raw ? JSON.parse(raw) : [];
    return Array.isArray(hist) ? hist : [];
  } catch {
    return [];
  }
}

const commands = ref([]);
let loaded = false;
async function loadCommands() {
  if (loaded) return;
  loaded = true;
  const list = await API.commands();
  commands.value = Array.isArray(list) ? list : [];
}

/** 每个控制台窗口实例一个会话 id（setup 时调用，多实例窗口互不相同）。 */
export function newSessionId() {
  return 'w' + Date.now().toString(36) + Math.random().toString(36).slice(2, 8);
}

export function useCommandInput(appKey) {
  const history = ref(loadHistory(appKey));
  let historyIndex = history.value.length;

  function pushHistory(v) {
    history.value.push(v);
    if (history.value.length > HISTORY_MAX) history.value = history.value.slice(-HISTORY_MAX);
    historyIndex = history.value.length;
    try {
      localStorage.setItem(historyKey(appKey), JSON.stringify(history.value));
    } catch { /* 隐私模式忽略 */ }
  }

  function historyUp(current) {
    if (history.value.length && historyIndex > 0) {
      historyIndex--;
      return history.value[historyIndex];
    }
    return current;
  }

  function historyDown() {
    if (historyIndex < history.value.length - 1) {
      historyIndex++;
      return history.value[historyIndex];
    }
    historyIndex = history.value.length;
    return '';
  }

  /**
   * 执行命令：附带本控制台窗口的会话 id——后端把命令执行期间的日志标记回这个
   * 会话，流回本窗口显示；其他窗口/日志应用互不干扰。
   */
  async function runCommand(raw, sessionId) {
    const v = raw.trim();
    if (!v) return null;
    pushHistory(v);
    return API.run(v, sessionId);
  }

  loadCommands();
  return { commands, historyUp, historyDown, runCommand };
}
