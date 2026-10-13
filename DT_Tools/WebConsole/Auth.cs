using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DT_Tools.WebConsole
{
    /// <summary>
    /// 访问鉴权。
    /// </summary>
    /// <remarks>
    /// 密码为空 → 完全放行（本机回环使用场景）；
    /// 非空 → 登录校验密码后发放会话 token（HttpOnly cookie），
    /// 后续请求凭 cookie dt_token 或 ?token= 校验【token】而非明文密码。
    /// </remarks>
    public sealed class Auth
    {
        private readonly string _password;
        private readonly string _token = Guid.NewGuid().ToString("N");

        public Auth(string password)
        {
            _password = (password ?? "").Trim();
        }

        private bool Disabled => _password.Length == 0;

        /// <summary>
        /// POST /login：校验密码（query token 或 body），成功则发放会话 cookie。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
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

        /// <summary>
        /// 请求鉴权检查；未通过时由 Router 回 401 + 登录页。
        /// </summary>
        /// <param name="ctx">HTTP 请求上下文，请求与响应都通过它读写。</param>
        /// <returns>鉴权通过时为 true；未通过时由路由返回 401。</returns>
        public bool Check(HttpListenerContext ctx)
        {
            if (Disabled)
                return true;
            var req = ctx.Request;
            // MCP 客户端 / 本机脚本通道：Authorization: Bearer <Password>（常量时间比较），
            // 免去浏览器登录换 cookie 的流程。仅接受明文密码本身，与 /login 同级。
            string auth = req.Headers["Authorization"];
            if (auth != null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return FixedTimeEquals(auth.Substring("Bearer ".Length).Trim(), _password);
            return CheckToken(req.Cookies["dt_token"]?.Value ?? "", req.QueryString["token"] ?? "");
        }

        /// <summary>
        /// 裸凭据校验：WebSocket 独立通道（WsServer）没有 HttpListenerContext，
        /// 由它从原始握手头解析出 cookie/token 后调用，语义与 Check 完全一致。
        /// </summary>
        /// <param name="cookieToken">会话 cookie 中的凭据。</param>
        /// <param name="queryToken">查询串中的凭据。</param>
        /// <returns>任一凭据有效时为 true。</returns>
        public bool CheckToken(string cookieToken, string queryToken)
        {
            if (Disabled)
                return true;
            return FixedTimeEquals(queryToken, _token) || FixedTimeEquals(cookieToken, _token);
        }

        /// <summary>
        /// 常量时间字符串比较（netstandard2.1 CryptographicOperations），
        /// 避免逐字符短路比较泄漏密码/token 前缀长度信息。
        /// </summary>
        /// <param name="a">比较的第一个字符串。</param>
        /// <param name="b">比较的第二个字符串。</param>
        /// <returns>两个字符串相等时为 true。</returns>
        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] ba = Encoding.UTF8.GetBytes(a ?? "");
            byte[] bb = Encoding.UTF8.GetBytes(b ?? "");
            return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}
