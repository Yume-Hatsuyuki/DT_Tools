using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using DT_Tools.Console.Commands;
using UnityEngine;

namespace DT_Tools.Console
{
    /// <summary>
    /// 内嵌 HTTP 服务器，在本地端口提供控制台 WebUI。
    /// 线程模型：
    ///   - HttpListener 跑在独立线程（_listenerThread）
    ///   - 命令执行通过 _pendingCommands 队列转发回 Unity 主线程（由 Update() 消费）
    ///   - 输出日志线程安全地写入 _log 循环缓冲区
    /// </summary>
    internal class WebConsole : MonoBehaviour
    {
        // ── 单例 ─────────────────────────────────────────────
        public static WebConsole Instance { get; private set; }

        // ── 配置 ─────────────────────────────────────────────
        internal static ConfigEntry<bool>   CfgEnabled;
        internal static ConfigEntry<int>    CfgPort;
        internal static ConfigEntry<string> CfgPassword;   // 空 = 不鉴权

        // ── 日志环形缓冲（线程安全） ─────────────────────────
        private const int LOG_CAPACITY = 200;
        private readonly List<LogEntry> _log      = new List<LogEntry>(LOG_CAPACITY);
        private readonly object          _logLock  = new object();

        // ── 命令队列（listener→主线程） ──────────────────────
        private readonly Queue<string>   _pending  = new Queue<string>();
        private readonly object          _pendLock = new object();

        // ── HTTP 监听器 ───────────────────────────────────────
        private HttpListener _listener;
        private Thread       _listenerThread;
        private bool         _running;

        // ── 命令注册表 ───────────────────────────────────────
        private readonly Dictionary<string, IConsoleCommand> _commands =
            new Dictionary<string, IConsoleCommand>(StringComparer.OrdinalIgnoreCase);

        private ManualLogSource _log4bep;

        // ═════════════════════════════════════════════════════
        //  初始化
        // ═════════════════════════════════════════════════════

        internal void Init(ManualLogSource bepLog)
        {
            if (Instance != null) { Destroy(this); return; }
            Instance  = this;
            _log4bep  = bepLog;
            DontDestroyOnLoad(gameObject);

            // 自动扫描并注册所有 IConsoleCommand 实现
            RegisterDiscoveredCommands();

            StartServer();
        }

        /// <summary>
        /// 反射扫描本程序集中实现 <see cref="IConsoleCommand"/> 的具体类并注册。
        /// <list type="bullet">
        ///   <item>优先匹配构造函数 <c>ctor(Dictionary&lt;string, IConsoleCommand&gt;)</c>（如 HelpCommand，注入注册表）。</item>
        ///   <item>否则要求无参构造函数。</item>
        ///   <item>抽象类 / 接口 / 无法实例化的类型会被跳过。</item>
        /// </list>
        /// 新增命令只需实现接口并放在程序集内，无需再改此处。
        /// </summary>
        private void RegisterDiscoveredCommands()
        {
            var commandType = typeof(IConsoleCommand);
            var registryCtorParam = typeof(Dictionary<string, IConsoleCommand>);

            var types = GetType().Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && commandType.IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal); // 稳定顺序，避免反射顺序抖动

            foreach (var type in types)
            {
                IConsoleCommand instance = null;

                // HelpCommand 等需要注入注册表的命令
                var registryCtor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { registryCtorParam },
                    null);
                if (registryCtor != null)
                {
                    instance = (IConsoleCommand)registryCtor.Invoke(new object[] { _commands });
                }
                else
                {
                    var emptyCtor = type.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null);
                    if (emptyCtor == null)
                    {
                        Log($"[WebConsole] 跳过命令类型 {type.Name}：无可用构造函数。", LogLevel.Warning);
                        continue;
                    }
                    instance = (IConsoleCommand)emptyCtor.Invoke(null);
                }

                Register(instance);
                Log($"[WebConsole] 已注册命令 /{instance.Name}", LogLevel.Debug);
            }
        }

        public void Register(IConsoleCommand cmd)
        {
            _commands[cmd.Name] = cmd;
            foreach (var alias in cmd.Aliases)
                _commands[alias] = cmd;
        }

        // ═════════════════════════════════════════════════════
        //  HTTP 服务器
        // ═════════════════════════════════════════════════════

        private void StartServer()
        {
            int port = CfgPort.Value;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                _listener.Start();
                _running = true;
                _listenerThread = new Thread(ListenerLoop) { IsBackground = true, Name = "DT_WebConsole" };
                _listenerThread.Start();
                Log($"[WebConsole] 已启动 → http://127.0.0.1:{port}/", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log($"[WebConsole] 启动失败: {ex.Message}", LogLevel.Error);
            }
        }

        private void StopServer()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        private void ListenerLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { break; }

                try { HandleRequest(ctx); }
                catch (Exception ex)
                {
                    Log($"[WebConsole] 请求处理错误: {ex.Message}", LogLevel.Warning);
                }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;
            resp.AddHeader("Access-Control-Allow-Origin", "*");

            // 简单密码鉴权（cookie 或 query token）
            string pass = CfgPassword.Value?.Trim();
            if (!string.IsNullOrEmpty(pass))
            {
                string token = req.QueryString["token"] ?? "";
                string cookie = req.Cookies["dt_token"]?.Value ?? "";
                if (token != pass && cookie != pass)
                {
                    if (req.Url.AbsolutePath == "/login" && req.HttpMethod == "POST")
                    {
                        string body = ReadBody(req);
                        if (body == pass)
                        {
                            resp.SetCookie(new Cookie("dt_token", pass, "/"));
                            WriteText(resp, "ok");
                            return;
                        }
                    }
                    WriteHtml(resp, LoginPage(), 401);
                    return;
                }
            }

            string path = req.Url.AbsolutePath;

            if (path == "/" || path == "/index.html")
            {
                WriteHtml(resp, BuildWebUI());
                return;
            }

            if (path == "/api/run" && req.HttpMethod == "POST")
            {
                string cmd = ReadBody(req).Trim();
                if (!string.IsNullOrEmpty(cmd))
                    EnqueueCommand(cmd);
                WriteJson(resp, "{\"ok\":true}");
                return;
            }

            if (path == "/api/log")
            {
                // ?since=N  — 只返回 seq > N 的条目
                int since = 0;
                if (!int.TryParse(req.QueryString["since"], out since)) since = 0;
                WriteJson(resp, BuildLogJson(since));
                return;
            }

            if (path == "/api/commands")
            {
                WriteJson(resp, BuildCommandsJson());
                return;
            }

            resp.StatusCode = 404;
            WriteText(resp, "Not Found");
        }

        // ═════════════════════════════════════════════════════
        //  Unity 主线程消费队列
        // ═════════════════════════════════════════════════════

        private void Update()
        {
            while (true)
            {
                string cmd;
                lock (_pendLock)
                {
                    if (_pending.Count == 0) break;
                    cmd = _pending.Dequeue();
                }
                ExecuteCommand(cmd);
            }
        }

        private void EnqueueCommand(string raw)
        {
            lock (_pendLock) _pending.Enqueue(raw);
        }

        // ═════════════════════════════════════════════════════
        //  命令执行（主线程）
        // ═════════════════════════════════════════════════════

        private void ExecuteCommand(string raw)
        {
            // 去掉前导 / 或 !
            if (raw.StartsWith("/") || raw.StartsWith("!"))
                raw = raw.Substring(1);

            if (string.IsNullOrWhiteSpace(raw)) return;

            var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string name = parts[0];

            if (!_commands.TryGetValue(name, out var cmd))
            {
                Log($"未知命令: {name}  （输入 /? 或 /help 查看帮助）", LogLevel.Warning);
                return;
            }

            string[] args = parts.Length > 1
                ? parts[1..]          // C# 8+，netstandard2.1 + LangVersion=latest 均支持
                : Array.Empty<string>();
            try
            {
                cmd.Execute(args, this);
            }
            catch (Exception ex)
            {
                Log($"[{name}] 执行出错: {ex.Message}", LogLevel.Error);
            }
        }

        // ═════════════════════════════════════════════════════
        //  日志
        // ═════════════════════════════════════════════════════

        private int _logSeq;

        public void Log(string message, LogLevel level = LogLevel.Message)
        {
            // 同时输出到 BepInEx 控制台
            _log4bep?.Log(level, message);

            lock (_logLock)
            {
                _logSeq++;
                if (_log.Count >= LOG_CAPACITY) _log.RemoveAt(0);
                _log.Add(new LogEntry { Seq = _logSeq, Time = DateTime.Now, Level = level, Message = message });
            }
        }

        private string BuildLogJson(int since)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            lock (_logLock)
            {
                foreach (var e in _log)
                {
                    if (e.Seq <= since) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    string color = e.Level switch
                    {
                        LogLevel.Error   => "red",
                        LogLevel.Warning => "orange",
                        LogLevel.Info    => "cyan",
                        _                => "white"
                    };
                    sb.Append($"{{\"seq\":{e.Seq},\"time\":\"{e.Time:HH:mm:ss}\",\"color\":\"{color}\",\"msg\":{JsonEscape(e.Message)}}}");
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        private string BuildCommandsJson()
        {
            // 返回去重后的命令详情（主名 + 别名 + 用法 + 描述 + 作者），供前端预览/补全
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<IConsoleCommand>();
            foreach (var kv in _commands)
            {
                if (kv.Key != kv.Value.Name) continue; // 只取主名条目
                if (!seen.Add(kv.Value.Name)) continue;
                list.Add(kv.Value);
            }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var sb = new StringBuilder("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var c = list[i];
                sb.Append('{');
                sb.Append("\"name\":").Append(JsonEscape(c.Name));
                sb.Append(",\"aliases\":[");
                for (int j = 0; j < c.Aliases.Length; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(JsonEscape(c.Aliases[j]));
                }
                sb.Append(']');
                sb.Append(",\"usage\":").Append(JsonEscape(c.Usage));
                sb.Append(",\"description\":").Append(JsonEscape(c.Description));
                sb.Append(",\"author\":").Append(JsonEscape(c.Author ?? ""));
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════
        //  WebUI HTML
        // ═════════════════════════════════════════════════════

        private static string BuildWebUI() => @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>DT Console</title>
<style>
  :root {
    --bg: #0c0c0c;
    --bg-panel: #141414;
    --bg-input: #1a1a1a;
    --border: #2a2a2a;
    --text: #c8c8c8;
    --text-dim: #666666;
    --green: #4e9a06;
    --green-bright: #8ae234;
    --amber: #c4a000;
    --red: #ef2929;
    --cyan: #34e2e2;
    --cmd: #8ae234;
    --suggest-bg: #1a1a1a;
    --suggest-hover: #243024;
    --suggest-sel: #2a3a2a;
    --radius: 4px;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  html, body { height: 100%; }
  body {
    background: var(--bg);
    color: var(--text);
    font-family: ""Segoe UI"", ""PingFang SC"", ""Microsoft YaHei"", system-ui, sans-serif;
    display: flex;
    flex-direction: column;
    line-height: 1.5;
    -webkit-font-smoothing: antialiased;
  }
  #toolbar {
    background: var(--bg-panel);
    border-bottom: 1px solid var(--border);
    padding: 8px 14px;
    display: flex;
    align-items: center;
    gap: 10px;
    flex-shrink: 0;
  }
  #toolbar h1 {
    font-size: 13px;
    font-weight: 600;
    color: var(--green-bright);
    letter-spacing: 1px;
    font-family: ""Cascadia Code"", ""Consolas"", monospace;
  }
  #status {
    margin-left: auto;
    font-size: 12px;
    color: var(--text-dim);
    display: flex;
    align-items: center;
    gap: 6px;
    font-family: ""Cascadia Code"", ""Consolas"", monospace;
  }
  #status .dot {
    width: 7px; height: 7px;
    border-radius: 50%;
    background: var(--text-dim);
  }
  #status.online .dot { background: var(--green-bright); }
  #status.offline .dot { background: var(--red); }
  #log {
    flex: 1;
    overflow-y: auto;
    padding: 12px 14px;
    font-family: ""Cascadia Code"", ""JetBrains Mono"", ""Consolas"", ""Courier New"", monospace;
    font-size: 13px;
    line-height: 1.6;
    background: var(--bg);
  }
  #log::-webkit-scrollbar { width: 8px; }
  #log::-webkit-scrollbar-track { background: transparent; }
  #log::-webkit-scrollbar-thumb { background: #333; border-radius: 4px; }
  .entry { white-space: pre-wrap; word-break: break-word; }
  .ts { color: var(--text-dim); margin-right: 8px; font-size: 12px; user-select: none; }
  #bar-wrap {
    position: relative;
    flex-shrink: 0;
  }
  #suggest {
    display: none;
    position: absolute;
    bottom: 100%;
    left: 12px;
    right: 12px;
    max-height: 280px;
    overflow-y: auto;
    background: var(--suggest-bg);
    border: 1px solid var(--border);
    border-bottom: none;
    border-radius: var(--radius) var(--radius) 0 0;
    z-index: 100;
    box-shadow: 0 -4px 16px rgba(0,0,0,0.5);
  }
  #suggest.open { display: block; }
  #suggest::-webkit-scrollbar { width: 6px; }
  #suggest::-webkit-scrollbar-thumb { background: #444; border-radius: 3px; }
  .sug-item {
    padding: 7px 12px;
    cursor: pointer;
    display: flex;
    align-items: baseline;
    gap: 10px;
    border-bottom: 1px solid #222;
    font-family: ""Cascadia Code"", ""Consolas"", monospace;
    font-size: 13px;
  }
  .sug-item:last-child { border-bottom: none; }
  .sug-item:hover { background: var(--suggest-hover); }
  .sug-item.sel { background: var(--suggest-sel); }
  .sug-name { color: var(--green-bright); font-weight: 600; min-width: 110px; flex-shrink: 0; }
  .sug-desc { color: var(--text-dim); flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; }
  .sug-author { font-size: 11px; flex-shrink: 0; }
  .sug-author-label { color: var(--text-dim); }
  .sug-author-name { color: var(--amber); }
  #bar {
    display: flex;
    background: var(--bg-panel);
    border-top: 1px solid var(--border);
    padding: 10px 12px;
    gap: 8px;
    align-items: center;
  }
  #input {
    flex: 1;
    background: var(--bg-input);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    color: var(--text);
    font-family: ""Cascadia Code"", ""Consolas"", monospace;
    font-size: 13px;
    padding: 8px 12px;
    outline: none;
  }
  #input::placeholder { color: var(--text-dim); }
  #input:focus { border-color: var(--green); }
  #send {
    background: var(--green);
    color: #0c0c0c;
    border: none;
    border-radius: var(--radius);
    padding: 8px 16px;
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    font-family: inherit;
  }
  #send:hover { background: var(--green-bright); }
</style>
</head>
<body>
<div id=""toolbar"">
  <h1>DT CONSOLE</h1>
  <div id=""status""><span class=""dot""></span><span id=""status-text"">连接中…</span></div>
</div>
<div id=""log""></div>
<div id=""bar-wrap"">
  <div id=""suggest""></div>
  <div id=""bar"">
    <input id=""input"" placeholder=""输入 / 查看命令，上下键选择，Enter 执行"" autocomplete=""off"" spellcheck=""false"">
    <button id=""send"">发送</button>
  </div>
</div>
<script>
const log = document.getElementById('log');
const inp = document.getElementById('input');
const statusEl = document.getElementById('status');
const statusText = document.getElementById('status-text');
const sugEl = document.getElementById('suggest');
let seq = 0;
let hist = [], hIdx = -1;
let commands = [];   // [{name,aliases,usage,description,author}, ...]
let matches = [];    // current filtered list
let selIdx = -1;     // selected index in matches

async function loadCommands(){
  try{
    const r = await fetch('/api/commands');
    commands = await r.json();
  }catch{ commands = []; }
}

function getPartial(){
  const val = inp.value;
  // 只对第一个 token 做建议（命令名）
  const m = val.match(/^([\/!]?)(\S*)$/);
  if(!m) return null;
  return { prefix: m[1], partial: m[2].toLowerCase() };
}

function filterCommands(){
  const p = getPartial();
  if(p === null){ hideSuggest(); return; }
  const { partial } = p;
  // 空 partial 或只有 / 时显示全部；否则按 name/aliases 前缀匹配
  matches = commands.filter(c => {
    if(!partial) return true;
    if(c.name.toLowerCase().startsWith(partial)) return true;
    return (c.aliases||[]).some(a => a.toLowerCase().startsWith(partial));
  });
  if(matches.length === 0){ hideSuggest(); return; }
  selIdx = 0;
  renderSuggest();
}

function renderSuggest(){
  sugEl.innerHTML = '';
  matches.forEach((c, i) => {
    const d = document.createElement('div');
    d.className = 'sug-item' + (i === selIdx ? ' sel' : '');
    d.innerHTML =
      '<span class=""sug-name"">/' + escHtml(c.name) + '</span>' +
      '<span class=""sug-desc"">' + escHtml(c.description || c.usage || '') + '</span>' +
      (c.author ? '<span class=""sug-author""><span class=""sug-author-label"">功能制作者：</span><span class=""sug-author-name"">' + escHtml(c.author) + '</span></span>' : '');
    d.onmousedown = (e) => { e.preventDefault(); applyMatch(i); };
    sugEl.appendChild(d);
  });
  sugEl.classList.add('open');
  // 滚动到选中项
  const sel = sugEl.children[selIdx];
  if(sel) sel.scrollIntoView({ block: 'nearest' });
}

function hideSuggest(){
  sugEl.classList.remove('open');
  matches = [];
  selIdx = -1;
}

function applyMatch(idx){
  if(idx < 0 || idx >= matches.length) return;
  const c = matches[idx];
  const p = getPartial();
  const prefix = (p && p.prefix) ? p.prefix : '/';
  inp.value = prefix + c.name + ' ';
  hideSuggest();
  inp.focus();
  const pos = inp.value.length;
  inp.setSelectionRange(pos, pos);
}

async function send(){
  const v = inp.value.trim();
  if(!v) return;
  hist.unshift(v); if(hist.length>50) hist.pop();
  hIdx = -1;
  hideSuggest();
  inp.value = '';
  appendLocal('> ' + v, 'var(--cmd)');
  await fetch('/api/run', { method: 'POST', body: v });
}

function appendLocal(msg, color){
  const d = document.createElement('div');
  d.className = 'entry';
  d.style.color = color || 'var(--text)';
  d.textContent = msg;
  log.appendChild(d);
  log.scrollTop = log.scrollHeight;
}

async function poll(){
  try{
    const r = await fetch('/api/log?since=' + seq);
    const arr = await r.json();
    arr.forEach(e => {
      seq = Math.max(seq, e.seq);
      const d = document.createElement('div');
      d.className = 'entry';
      const colorMap = { red:'var(--red)', orange:'var(--amber)', cyan:'var(--cyan)', white:'var(--text)' };
      d.innerHTML = '<span class=""ts"">' + e.time + '</span><span style=""color:' + (colorMap[e.color]||e.color) + '"">' + escHtml(e.msg) + '</span>';
      log.appendChild(d);
    });
    if(arr.length) log.scrollTop = log.scrollHeight;
    statusEl.className = 'online';
    statusText.textContent = '在线';
  }catch{
    statusEl.className = 'offline';
    statusText.textContent = '离线';
  }
  setTimeout(poll, 800);
}

function escHtml(s){
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

document.getElementById('send').onclick = send;

inp.addEventListener('input', () => {
  hIdx = -1;
  filterCommands();
});

inp.addEventListener('keydown', e => {
  const open = sugEl.classList.contains('open');

  if(e.key === 'ArrowUp'){
    e.preventDefault();
    if(open && matches.length){
      selIdx = (selIdx - 1 + matches.length) % matches.length;
      renderSuggest();
    } else {
      hIdx = Math.min(hIdx + 1, hist.length - 1);
      inp.value = hist[hIdx] || '';
      hideSuggest();
    }
    return;
  }
  if(e.key === 'ArrowDown'){
    e.preventDefault();
    if(open && matches.length){
      selIdx = (selIdx + 1) % matches.length;
      renderSuggest();
    } else {
      hIdx = Math.max(hIdx - 1, -1);
      inp.value = hIdx < 0 ? '' : hist[hIdx];
      hideSuggest();
    }
    return;
  }
  if(e.key === 'Tab'){
    e.preventDefault();
    if(open && matches.length && selIdx >= 0){
      applyMatch(selIdx);
    } else {
      filterCommands();
      if(matches.length === 1) applyMatch(0);
    }
    return;
  }
  if(e.key === 'Enter'){
    e.preventDefault();
    if(open && matches.length && selIdx >= 0){
      // 若输入只有命令前缀（无空格参数），应用选中项后再发送；有参数则直接发
      const val = inp.value.trim();
      const hasArgs = /\s+\S/.test(val);
      if(!hasArgs){
        applyMatch(selIdx);
        // 稍等 DOM 更新后发送
        setTimeout(send, 0);
        return;
      }
    }
    send();
    return;
  }
  if(e.key === 'Escape'){
    hideSuggest();
    return;
  }
});

inp.addEventListener('blur', () => {
  // 延迟关闭，允许 mousedown 先触发
  setTimeout(hideSuggest, 150);
});

inp.addEventListener('focus', () => {
  filterCommands();
});

loadCommands().then(() => filterCommands());
poll();
inp.focus();
</script>
</body>
</html>";

        private static string LoginPage() => @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>DT Console · 登录</title>
<style>
  :root {
    --bg: #0c0c0c;
    --card: #141414;
    --border: #2a2a2a;
    --text: #c8c8c8;
    --text-dim: #666;
    --green: #4e9a06;
    --green-bright: #8ae234;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    background: var(--bg);
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    font-family: ""Segoe UI"", ""PingFang SC"", ""Microsoft YaHei"", system-ui, sans-serif;
    color: var(--text);
  }
  .box {
    background: var(--card);
    padding: 28px 24px;
    border: 1px solid var(--border);
    border-radius: 6px;
    width: 300px;
  }
  h2 {
    text-align: center;
    font-size: 15px;
    font-weight: 600;
    color: var(--green-bright);
    font-family: ""Cascadia Code"", ""Consolas"", monospace;
    letter-spacing: 1px;
    margin-bottom: 6px;
  }
  .sub {
    text-align: center;
    font-size: 12px;
    color: var(--text-dim);
    margin-bottom: 18px;
  }
  input {
    width: 100%;
    background: #1a1a1a;
    border: 1px solid var(--border);
    color: var(--text);
    padding: 9px 12px;
    font-family: ""Cascadia Code"", ""Consolas"", monospace;
    font-size: 13px;
    border-radius: 4px;
    margin-bottom: 12px;
    outline: none;
  }
  input:focus { border-color: var(--green); }
  input::placeholder { color: var(--text-dim); }
  button {
    width: 100%;
    background: var(--green);
    color: #0c0c0c;
    border: none;
    padding: 9px;
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    border-radius: 4px;
  }
  button:hover { background: var(--green-bright); }
</style>
</head>
<body>
<div class=""box"">
  <h2>DT CONSOLE</h2>
  <p class=""sub"">请输入访问密码</p>
  <input type=""password"" id=""p"" placeholder=""密码"" autofocus>
  <button onclick=""login()"">登录</button>
</div>
<script>
async function login(){
  const v = document.getElementById('p').value;
  const r = await fetch('/login?token='+encodeURIComponent(v),{method:'POST',body:v});
  if(await r.text()==='ok') location.href='/';
  else alert('密码错误');
}
document.getElementById('p').addEventListener('keydown', e=>{ if(e.key==='Enter') login(); });
</script>
</body>
</html>";

        // ═════════════════════════════════════════════════════
        //  HTTP 工具
        // ═════════════════════════════════════════════════════

        private static string ReadBody(HttpListenerRequest req)
        {
            using var sr = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
            return sr.ReadToEnd();
        }

        private static void WriteHtml(HttpListenerResponse resp, string html, int code = 200)
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            resp.StatusCode = code;
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }

        private static void WriteJson(HttpListenerResponse resp, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            resp.StatusCode = 200;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }

        private static void WriteText(HttpListenerResponse resp, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            resp.StatusCode = 200;
            resp.ContentType = "text/plain; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }

        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ═════════════════════════════════════════════════════
        //  生命周期
        // ═════════════════════════════════════════════════════

        private void OnDestroy()
        {
            StopServer();
            if (Instance == this) Instance = null;
        }

        // ─── 内部类型 ─────────────────────────────────────────
        private struct LogEntry
        {
            public int      Seq;
            public DateTime Time;
            public LogLevel Level;
            public string   Message;
        }
    }
}
