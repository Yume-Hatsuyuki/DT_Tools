import { ref } from 'vue';
import { API } from '../../api.js';

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
    } catch { }
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

  async function runCommand(raw, sessionId) {
    const v = raw.trim();
    if (!v) return null;
    pushHistory(v);
    return API.run(v, sessionId);
  }

  loadCommands();
  return { commands, historyUp, historyDown, runCommand };
}
