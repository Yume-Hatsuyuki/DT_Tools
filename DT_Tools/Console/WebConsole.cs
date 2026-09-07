using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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

            // 注册内置命令
            Register(new HelpCommand(_commands));
            Register(new GiveDrinkCommand());
            Register(new ListPlayersCommand());

            StartServer();
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
            // 去重后返回主名 + 所有别名，供前端 Tab 补全
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new List<string>();
            foreach (var kv in _commands)
            {
                if (seen.Add(kv.Key))
                    names.Add(kv.Key);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder("[");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonEscape(names[i]));
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
  #bar {
    display: flex;
    background: var(--bg-panel);
    border-top: 1px solid var(--border);
    padding: 10px 12px;
    gap: 8px;
    flex-shrink: 0;
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
<div id=""bar"">
  <input id=""input"" placeholder=""输入命令，/help 查看帮助"" autocomplete=""off"" spellcheck=""false"">
  <button id=""send"">发送</button>
</div>
<script>
const log = document.getElementById('log');
const inp = document.getElementById('input');
const statusEl = document.getElementById('status');
const statusText = document.getElementById('status-text');
let seq = 0;
let hist = [], hIdx = -1;
let commands = [];

async function loadCommands(){
  try{
    const r = await fetch('/api/commands');
    commands = await r.json();
  }catch{ commands = []; }
}

async function send(){
  const v = inp.value.trim();
  if(!v) return;
  hist.unshift(v); if(hist.length>50) hist.pop();
  hIdx = -1;
  inp.value='';
  appendLocal('> '+v, 'var(--cmd)');
  await fetch('/api/run',{method:'POST',body:v});
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
    const r = await fetch('/api/log?since='+seq);
    const arr = await r.json();
    arr.forEach(e=>{
      seq = Math.max(seq, e.seq);
      const d = document.createElement('div');
      d.className = 'entry';
      const colorMap = { red:'var(--red)', orange:'var(--amber)', cyan:'var(--cyan)', white:'var(--text)' };
      d.innerHTML = '<span class=""ts"">'+e.time+'</span><span style=""color:'+(colorMap[e.color]||e.color)+'"">'+escHtml(e.msg)+'</span>';
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
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// Tab 补全：补全第一个 token（命令名），支持前导 / 或 !
function tabComplete(){
  const val = inp.value;
  const caret = inp.selectionStart ?? val.length;
  const before = val.slice(0, caret);
  const after = val.slice(caret);
  // 只补全第一个词
  const m = before.match(/^([\/!]?)(\S*)$/);
  if(!m) return;
  const prefix = m[1];          // / 或 ! 或空
  const partial = m[2].toLowerCase();
  if(commands.length === 0) return;
  const matches = commands.filter(c => c.toLowerCase().startsWith(partial));
  if(matches.length === 0) return;
  if(matches.length === 1){
    inp.value = prefix + matches[0] + (after.startsWith(' ') ? after : ' ' + after.trimStart());
    const pos = (prefix + matches[0] + ' ').length;
    inp.setSelectionRange(pos, pos);
    return;
  }
  // 多个匹配：补公共前缀；若无进展则列出候选
  let common = matches[0];
  for(const m2 of matches.slice(1)){
    let i = 0;
    while(i < common.length && i < m2.length && common[i].toLowerCase() === m2[i].toLowerCase()) i++;
    common = common.slice(0, i);
  }
  if(common.length > partial.length){
    inp.value = prefix + common + after;
    const pos = (prefix + common).length;
    inp.setSelectionRange(pos, pos);
  } else {
    appendLocal(matches.map(c => prefix + c).join('  '), 'var(--text-dim)');
  }
}

document.getElementById('send').onclick = send;
inp.addEventListener('keydown', e=>{
  if(e.key === 'Enter'){ send(); return; }
  if(e.key === 'Tab'){ e.preventDefault(); tabComplete(); return; }
  if(e.key === 'ArrowUp'){ hIdx = Math.min(hIdx+1, hist.length-1); inp.value = hist[hIdx]||''; e.preventDefault(); }
  if(e.key === 'ArrowDown'){ hIdx = Math.max(hIdx-1, -1); inp.value = hIdx<0 ? '' : hist[hIdx]; e.preventDefault(); }
});
loadCommands();
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
