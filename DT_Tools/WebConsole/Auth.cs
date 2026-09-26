using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// 访问鉴权。密码为空 → 完全放行（本机回环使用场景）；
    /// 非空 → 登录校验密码后发放会话 token（HttpOnly cookie），
    /// 后续请求凭 cookie dt_token 或 ?token= 校验【token】而非明文密码。
    /// </summary>
    public sealed class Auth
    {
        private readonly string _password;
        private readonly string _token = Guid.NewGuid().ToString("N");

        public Auth(string password)
        {
            _password = (password ?? "").Trim();
        }

        private bool Disabled => _password.Length == 0;

        /// <summary>POST /login：校验密码（query token 或 body），成功则发放会话 cookie。</summary>
        public void HandleLogin(HttpListenerContext ctx)
        {
            var resp = ctx.Response;
            if (Disabled)
            {
                HttpServer.WriteText(resp, 200, "ok");
                return;
            }

            string queryToken = ctx.Request.QueryString["token"] ?? "";
            string body = HttpServer.ReadBody(ctx.Request).Trim();
            if (!FixedTimeEquals(queryToken, _password) && !FixedTimeEquals(body, _password))
            {
                Log.Warn("WebConsole", "登录失败：密码错误。");
                HttpServer.WriteText(resp, 401, "Unauthorized");
                return;
            }

            resp.SetCookie(new Cookie("dt_token", _token, "/") { HttpOnly = true });
            Log.Info("WebConsole", "登录成功。");
            HttpServer.WriteText(resp, 200, "ok");
        }

        /// <summary>请求鉴权检查；未通过时由 Router 回 401 + 登录页。</summary>
        public bool Check(HttpListenerContext ctx)
        {
            if (Disabled)
                return true;

            var req = ctx.Request;
            string queryToken = req.QueryString["token"] ?? "";
            string cookieToken = req.Cookies["dt_token"]?.Value ?? "";
            return FixedTimeEquals(queryToken, _token) || FixedTimeEquals(cookieToken, _token);
        }

        /// <summary>
        /// 常量时间字符串比较（netstandard2.1 CryptographicOperations），
        /// 避免逐字符短路比较泄漏密码/token 前缀长度信息。
        /// </summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] ba = Encoding.UTF8.GetBytes(a ?? "");
            byte[] bb = Encoding.UTF8.GetBytes(b ?? "");
            return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}
