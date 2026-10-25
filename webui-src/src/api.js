
async function request(path, opts = {}) {
  let res;
  try {
    res = await fetch(path, opts);
  } catch {
    return { ok: false, error: 'network error', offline: true };
  }
  if (res.status === 401) {
    location.href = '/login.html';
    return { ok: false, error: 'unauthorized', unauthorized: true };
  }
  const text = await res.text();
  try {
    return JSON.parse(text);
  } catch {
    return { ok: res.ok, error: text || 'HTTP ' + res.status };
  }
}

function post(path, body) {
  return request(path, {
    method: 'POST',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export const API = {
  run: (cmd, sessionId) => request('/api/run', {
    method: 'POST',
    headers: sessionId ? { 'X-DT-Session': sessionId } : undefined,
    body: cmd,
  }),
  logSince: (seq) => request('/api/log?since=' + seq),
  meta: () => request('/api/meta'),
  wsLogUrl: (wsPort) =>
    (location.protocol === 'https:' ? 'wss://' : 'ws://')
    + (wsPort ? location.hostname + ':' + wsPort : location.host)
    + '/api/log/ws',
  commands: () => request('/api/commands'),
  steamPlayers: () => request('/api/steam/players'),
  steamNews: (lang) => request('/api/steam/news?lang=' + encodeURIComponent(lang)),

  updateStatus: () => request('/api/update/status'),
  updateCheck: () => post('/api/update/check'),
  updateDownload: () => post('/api/update/download'),
  updateReveal: () => post('/api/update/reveal'),

  mcpStatus: () => request('/api/mcp/status'),
  mcpToggle: (enabled) => post('/api/mcp/toggle', { enabled }),

  fsList: (path) => request('/api/fs/list' + (path ? '?path=' + encodeURIComponent(path) : '')),

  gameExit: () => post('/api/game/exit'),

  configList: () => request('/api/config/list'),
  configTags: () => request('/api/config/tags'),
  configUpdate: (section, key, value) => post('/api/config/update', { section, key, value }),
  configSave: () => post('/api/config/save'),
  configReset: (section) => post('/api/config/reset', section ? { section } : {}),
  configImport: (format, mode, content) => post('/api/config/import', { format, mode, content }),
  exportCfgUrl: '/api/config/export.cfg',

  sectionLog: (section) => request('/api/config/section/' + encodeURIComponent(section) + '/log'),
  sectionLogClear: (section) => post('/api/config/section/' + encodeURIComponent(section) + '/log/clear'),

  automationStatus: () => request('/api/automation/status'),
  automationHost: (enabled) => post('/api/automation/host', { enabled }),
  moduleLog: (id) => request('/api/automation/modules/' + encodeURIComponent(id) + '/log'),
  moduleLogClear: (id) => post('/api/automation/modules/' + encodeURIComponent(id) + '/log/clear'),

  dummyState: () => request('/api/dummy/state'),
  dummyCharacters: () => request('/api/dummy/characters'),
  dummyCreate: (name, characterId) => post('/api/dummy/create', { name, characterId }),
  dummyRemove: (name) => post('/api/dummy/remove', { name }),
  dummyRemoveAll: () => post('/api/dummy/remove-all'),
  dummyReady: (name, ready) => post('/api/dummy/ready', { name, ready }),
  dummyPick: (name, characterId) => post('/api/dummy/pick', { name, characterId }),

  mp3State: () => request('/api/mp3/state'),
  mp3Play: (path, listId) => post('/api/mp3/play', { path, listId }),
  mp3Index: (index, listId) => post('/api/mp3/index', { index, listId }),
  mp3Next: (delta = 1) => post('/api/mp3/next', { delta }),
  mp3Pause: () => post('/api/mp3/pause'),
  mp3Resume: () => post('/api/mp3/resume'),
  mp3Stop: () => post('/api/mp3/stop'),
  mp3Seek: (position) => post('/api/mp3/seek', { position }),
  mp3Volume: (volume) => post('/api/mp3/volume', { volume }),
  mp3Mic: (enabled) => post('/api/mp3/mic', { enabled }),
  mp3Local: (enabled) => post('/api/mp3/local', { enabled }),
  mp3Mode: (mode) => post('/api/mp3/mode', { mode }),
  mp3PlaylistAdd: (path, listId, extra = {}) => post('/api/mp3/playlist/add', { path, listId, ...extra }),
  mp3PlaylistAlias: (index, alias, listId) => post('/api/mp3/playlist/alias', { index, alias, listId }),
  mp3PlaylistUpdate: (index, listId, fields = {}) => post('/api/mp3/playlist/update', { index, listId, ...fields }),
  mp3NeteaseInfo: (input, ipMode, fallbackIp) => post('/api/mp3/netease/info', { input, ipMode, fallbackIp }),
  mp3NeteaseSettings: () => request('/api/mp3/netease/settings'),
  mp3NeteaseSettingsSet: (settings) => post('/api/mp3/netease/settings', settings),
  mp3PlaylistRemove: (index, listId) => post('/api/mp3/playlist/remove', { index, listId }),
  mp3PlaylistMove: (from, to, listId) => post('/api/mp3/playlist/move', { from, to, listId }),
  mp3PlaylistClear: (listId) => post('/api/mp3/playlist/clear', { listId }),
  mp3ListSelect: (id) => post('/api/mp3/lists/select', { id }),
  mp3ListCreate: (name) => post('/api/mp3/lists/create', { name }),
  mp3ListRename: (id, name) => post('/api/mp3/lists/rename', { id, name }),
  mp3ListDelete: (id) => post('/api/mp3/lists/delete', { id }),
  gamepadState: () => request('/api/gamepad/state'),
  gamepadBind: (layer, button, output) => post('/api/gamepad/bind', { layer, button, output }),
  gamepadToggle: (button) => post('/api/gamepad/toggle', { button }),
  gamepadReset: () => post('/api/gamepad/reset'),
  tabletState: (since) => request('/api/tablet/state?since=' + since),
  tabletSend: (text) => post('/api/tablet/send', { text }),
  tabletPhoto: (data) => post('/api/tablet/photo', { data }),
  tabletPhotoUrl: (key) => '/api/tablet/photo?key=' + key,
  tabletClear: (scope) => post('/api/tablet/clear', { scope }),
  tabletPhotoBlob: async (key) => {
    const res = await fetch('/api/tablet/photo?key=' + key);
    if (!res.ok) throw new Error('照片读取失败');
    return res.blob();
  },
};

export function createPoller(fn, ms) {
  let timer = null;
  const tick = () => { if (!document.hidden) fn(); };
  const onVisible = () => { if (!document.hidden && timer) fn(); };
  return {
    start() {
      if (timer) return;
      document.addEventListener('visibilitychange', onVisible);
      tick();
      timer = setInterval(tick, ms);
    },
    stop() {
      if (!timer) return;
      document.removeEventListener('visibilitychange', onVisible);
      clearInterval(timer);
      timer = null;
    },
  };
}
