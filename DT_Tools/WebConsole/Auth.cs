using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DT_Tools.WebConsole
{
    public sealed class Auth
    {
        private readonly string _password;
        private readonly string _token = Guid.NewGuid().ToString("N");
        private readonly LoginThrottle _throttle = new LoginThrottle();

        public Auth(string password)
        {
            _password = (password ?? "").Trim();
        }

        private bool Disabled => _password.Length == 0;

        public void HandleLogin(HttpListenerContext ctx)
        {
            var resp = ctx.Response;
            if (Disabled)
            {
                HttpServer.WriteText(resp, 200, "ok");
                return;
            }

            string ip = ClientKey(ctx);
            DateTime now = DateTime.UtcNow;
            if (_throttle.IsLocked(ip, now, out TimeSpan wait))
            {
                resp.Headers["Retry-After"] = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString();
                Log.Warn("WebConsole", "登录已被临时锁定（失败次数过多），来源 " + ip + "。");
                HttpServer.WriteText(resp, 429, "Too Many Requests");
                return;
            }

            string body = HttpServer.ReadBody(ctx.Request, BodyLimit.Login).Trim();
            if (!FixedTimeEquals(body, _password))
            {
                _throttle.RecordFailure(ip, now);
                Log.Warn("WebConsole", "登录失败：密码错误（来源 " + ip + "）。");
                HttpServer.WriteText(resp, 401, "Unauthorized");
                return;
            }

            _throttle.RecordSuccess(ip);
            resp.AddHeader("Set-Cookie", "dt_token=" + _token + "; Path=/; HttpOnly; SameSite=Strict");
            Log.Info("WebConsole", "登录成功。");
            HttpServer.WriteText(resp, 200, "ok");
        }

        public bool Check(HttpListenerContext ctx)
        {
            if (Disabled)
                return true;
            var req = ctx.Request;
            string auth = req.Headers["Authorization"];
            if (auth != null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string ip = ClientKey(ctx);
                DateTime now = DateTime.UtcNow;
                if (_throttle.IsLocked(ip, now, out _))
                    return false;
                bool ok = FixedTimeEquals(auth.Substring("Bearer ".Length).Trim(), _password);
                if (ok)
                    _throttle.RecordSuccess(ip);
                else
                    _throttle.RecordFailure(ip, now);
                return ok;
            }
            return CheckToken(req.Cookies["dt_token"]?.Value ?? "", req.QueryString["token"] ?? "");
        }

        public bool CheckToken(string cookieToken, string queryToken)
        {
            if (Disabled)
                return true;
            return FixedTimeEquals(queryToken, _token) || FixedTimeEquals(cookieToken, _token);
        }

        private static string ClientKey(HttpListenerContext ctx)
        {
            var ep = ctx.Request.RemoteEndPoint;
            return ep != null && ep.Address != null ? ep.Address.ToString() : "unknown";
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] ba = Encoding.UTF8.GetBytes(a ?? "");
            byte[] bb = Encoding.UTF8.GetBytes(b ?? "");
            return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}
