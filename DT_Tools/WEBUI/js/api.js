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
  // 控制台
  run: (cmd) => request('/api/run', { method: 'POST', body: cmd }),
  logSince: (seq) => request('/api/log?since=' + seq),
  logStreamUrl: '/api/log/stream',
  commands: () => request('/api/commands'),
  steamPlayers: () => request('/api/steam/players'),

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
