// 作用：实现更新检查的 HTTP 接口；不负责路由注册与命令执行。
using System.Net;

namespace DT_Tools.WebConsole.Api
{
    public static class UpdateApi
    {
        public static void HandleStatus(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, UpdateService.GetStatus(false));

        public static void HandleCheck(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, UpdateService.GetStatus(true));

        public static void HandleDownload(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, UpdateService.StartDownload());

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
