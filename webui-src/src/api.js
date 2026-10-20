/**
 * 唯一请求层：路径常量、{ok,error,data} 信封解包、401 统一跳登录、轮询调度。
 * 视图代码只 import API，不允许出现裸 fetch 与字面量路径。
 */

async function request(path, opts = {}) {
  let res;
  try {
    res = await fetch(path, opts);
  } catch {
    return { ok: false, error: 'network error', offline: true };
  }
  if (res.status === 401) {
    // 会话失效（插件重启后 token 轮换）：统一送回登录页
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
  // 控制台：sessionId（可选）随头携带，后端把该命令执行期间的日志标记回这个会话，
  // 各控制台窗口只显示自己会话的输出（日志应用看全量）
  run: (cmd, sessionId) => request('/api/run', {
    method: 'POST',
    headers: sessionId ? { 'X-DT-Session': sessionId } : undefined,
    body: cmd,
  }),
  logSince: (seq) => request('/api/log?since=' + seq),
  /** 运行时能力发现（WebSocket 实时流独立端口；0=实时流禁用）。 */
  meta: () => request('/api/meta'),
  /**
   * 实时日志 WebSocket。wsPort 来自 /api/meta（Mono 的 HttpListener 不支持升级，
   * 插件在独立端口自管 WS 通道）；wsPort 为空时退回同源路径（旧插件行为，
   * 会快速 501 → 走轮询兜底）。断线重连时调用方追加 ?since=seq 续传。
   */
  wsLogUrl: (wsPort) =>
    (location.protocol === 'https:' ? 'wss://' : 'ws://')
    + (wsPort ? location.hostname + ':' + wsPort : location.host)
    + '/api/log/ws',
  commands: () => request('/api/commands'),
  steamPlayers: () => request('/api/steam/players'),
  /** 游戏新闻（Steam 新闻 RSS，后端中转）。lang 须为白名单内的 Steam 语言代码。 */
  steamNews: (lang) => request('/api/steam/news?lang=' + encodeURIComponent(lang)),

  // 更新检测与下载（数据源 GitHub Releases latest；status 走后端 3 分钟缓存，
  // check 手动绕过缓存，download 启动后台下载、进度经 status 轮询）
  updateStatus: () => request('/api/update/status'),
  updateCheck: () => post('/api/update/check'),
  updateDownload: () => post('/api/update/download'),
  updateReveal: () => post('/api/update/reveal'),

  // MCP 桥接（WebUI 挂件）：状态读取与开关（toggle 走后端 ConfigService，落盘 .cfg）
  mcpStatus: () => request('/api/mcp/status'),
  mcpToggle: (enabled) => post('/api/mcp/toggle', { enabled }),

  // 本机目录浏览（WebUI 内置文件选择器的数据源；path 为空=盘符根视图）
  fsList: (path) => request('/api/fs/list' + (path ? '?path=' + encodeURIComponent(path) : '')),

  // 桌面壳系统操作
  gameExit: () => post('/api/game/exit'),

  // 配置
  configList: () => request('/api/config/list'),
  configTags: () => request('/api/config/tags'),
  configUpdate: (section, key, value) => post('/api/config/update', { section, key, value }),
  configSave: () => post('/api/config/save'),
  configReset: (section) => post('/api/config/reset', section ? { section } : {}),
  configImport: (format, mode, content) => post('/api/config/import', { format, mode, content }),
  exportCfgUrl: '/api/config/export.cfg',

  // 功能段日志
  sectionLog: (section) => request('/api/config/section/' + encodeURIComponent(section) + '/log'),
  sectionLogClear: (section) => post('/api/config/section/' + encodeURIComponent(section) + '/log/clear'),

  // 自动化
  automationStatus: () => request('/api/automation/status'),
  automationHost: (enabled) => post('/api/automation/host', { enabled }),
  moduleLog: (id) => request('/api/automation/modules/' + encodeURIComponent(id) + '/log'),
  moduleLogClear: (id) => post('/api/automation/modules/' + encodeURIComponent(id) + '/log/clear'),

  // 假人管理（错误码在 error，中文详情在 data.message）
  dummyState: () => request('/api/dummy/state'),
  dummyCharacters: () => request('/api/dummy/characters'),
  dummyCreate: (name, characterId) => post('/api/dummy/create', { name, characterId }),
  dummyRemove: (name) => post('/api/dummy/remove', { name }),
  dummyRemoveAll: () => post('/api/dummy/remove-all'),
  dummyReady: (name, ready) => post('/api/dummy/ready', { name, ready }),
  dummyPick: (name, characterId) => post('/api/dummy/pick', { name, characterId }),

  // 随身MP3（服务端播放器：本地出声 + 可选混入麦克风；歌单在 BepInEx/config/DT_Tools/mp3/ 持久化）
  mp3State: () => request('/api/mp3/state'),
  // listId 缺省时后端使用当前歌单；传入则只作用于该歌单（undefined 会被 JSON 忽略）
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
  // 手柄应用：只做配置与实时显示（手柄本身由后台线程处理，只控制游戏）
  gamepadState: () => request('/api/gamepad/state'),
  gamepadBind: (layer, button, output) => post('/api/gamepad/bind', { layer, button, output }),
  gamepadToggle: (button) => post('/api/gamepad/toggle', { button }),
  gamepadReset: () => post('/api/gamepad/reset'),
  // 平板电脑应用（实验性）：增量拉取（since 为已收到的最大序号）、发言、照片上传与读取、清理
  tabletState: (since) => request('/api/tablet/state?since=' + since),
  tabletSend: (text) => post('/api/tablet/send', { text }),
  tabletPhoto: (data) => post('/api/tablet/photo', { data }),
  tabletPhotoUrl: (key) => '/api/tablet/photo?key=' + key,
  /** scope：chat（聊天与照片）| clues（线索）| all（全部）。 */
  tabletClear: (scope) => post('/api/tablet/clear', { scope }),
  /** 照片原始 JPEG（用于复制与另存为）；失败时抛错。 */
  tabletPhotoBlob: async (key) => {
    const res = await fetch('/api/tablet/photo?key=' + key);
    if (!res.ok) throw new Error('照片读取失败');
    return res.blob();
  },
};

/**
 * 轮询器：标签页隐藏时暂停，恢复可见时立即补一次。
 * start()/stop() 可反复调用；同一时刻同一 poller 只有一个 interval。
 */
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
