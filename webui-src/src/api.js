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

  // 随身MP3（服务端播放器：本地出声 + 可选混入麦克风；歌单在 BepInEx/config 持久化）
  mp3State: () => request('/api/mp3/state'),
  mp3Play: (path) => post('/api/mp3/play', { path }),
  mp3Index: (index) => post('/api/mp3/index', { index }),
  mp3Next: (delta = 1) => post('/api/mp3/next', { delta }),
  mp3Pause: () => post('/api/mp3/pause'),
  mp3Resume: () => post('/api/mp3/resume'),
  mp3Stop: () => post('/api/mp3/stop'),
  mp3Seek: (position) => post('/api/mp3/seek', { position }),
  mp3Volume: (volume) => post('/api/mp3/volume', { volume }),
  mp3Mic: (enabled) => post('/api/mp3/mic', { enabled }),
  mp3Local: (enabled) => post('/api/mp3/local', { enabled }),
  mp3Mode: (mode) => post('/api/mp3/mode', { mode }),
  mp3PlaylistAdd: (path) => post('/api/mp3/playlist/add', { path }),
  mp3PlaylistRemove: (index) => post('/api/mp3/playlist/remove', { index }),
  mp3PlaylistMove: (from, to) => post('/api/mp3/playlist/move', { from, to }),
  mp3PlaylistClear: () => post('/api/mp3/playlist/clear'),
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
