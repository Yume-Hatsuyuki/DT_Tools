import { reactive } from 'vue';

const STORAGE_KEY = 'dt_shell_user_v1';
const DEFAULTS = { user: 'root', hostname: 'DT_Tools', avatar: null };

function load() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const saved = raw ? JSON.parse(raw) : {};
    return {
      user: typeof saved.user === 'string' && saved.user.trim() ? saved.user : DEFAULTS.user,
      hostname: typeof saved.hostname === 'string' && saved.hostname.trim() ? saved.hostname : DEFAULTS.hostname,
      avatar: typeof saved.avatar === 'string' ? saved.avatar : null,
    };
  } catch {
    return { ...DEFAULTS };
  }
}

const state = reactive(load());

function persist() {
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(state)); } catch { }
}

function sanitizeShellName(v) {
  const s = String(v ?? '').trim();
  if (!s) return { ok: false, error: '名称不能为空' };
  if (/\s/.test(s)) return { ok: false, error: '名称不能包含空白字符' };
  if (s.length > 32) return { ok: false, error: '名称过长（≤32 字符）' };
  return { ok: true, value: s };
}

export function useShellUser() {
  function setUser(name) {
    const r = sanitizeShellName(name);
    if (!r.ok) return r;
    state.user = r.value;
    persist();
    return r;
  }
  function setHostname(name) {
    const r = sanitizeShellName(name);
    if (!r.ok) return r;
    state.hostname = r.value;
    persist();
    return r;
  }
  function setAvatar(dataUrl) {
    state.avatar = dataUrl || null;
    persist();
  }
  return { state, setUser, setHostname, setAvatar };
}
