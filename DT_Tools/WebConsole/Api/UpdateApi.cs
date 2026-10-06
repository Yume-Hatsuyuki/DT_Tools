using System.Net;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// /api/update/* — 更新检测与下载（数据源 GitHub Releases latest，经 UpdateService）。
    /// 与 SteamApi 同类：纯 HTTP/后台线程工作，不触碰 Unity API；鉴权由 Router 统一处理。
    /// </summary>
    public static class UpdateApi
    {
        /// <summary>GET /api/update/status — 检测状态（缓存 3 分钟内直接回缓存）。</summary>
        public static void HandleStatus(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, UpdateService.GetStatus(false));

        /// <summary>POST /api/update/check — 手动检查：绕过缓存立即请求 GitHub。</summary>
        public static void HandleCheck(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, UpdateService.GetStatus(true));

        /// <summary>POST /api/update/download — 启动新版 zip 后台下载，进度经 status 轮询。</summary>
        public static void HandleDownload(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, UpdateService.StartDownload());

        /// <summary>POST /api/update/reveal — 资源管理器定位已下载的 zip（仅 Windows）。</summary>
        public static void HandleReveal(HttpListenerContext ctx)
        {
            string error = UpdateService.RevealDownload();
            if (error == null)
                HttpServer.WriteJson(ctx.Response, new { ok = true });
            else
                HttpServer.WriteJson(ctx.Response, new { ok = false, error });
        }
    }
}
