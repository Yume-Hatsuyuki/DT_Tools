using System;
using System.Net;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.WebConsole.Api
{
    public static class SystemApi
    {
        public static void HandleExit(HttpListenerContext ctx)
        {
            Log.Info("WebConsole", "WebUI 请求退出游戏，3 秒后关闭。");
            WebConsole.Post(() =>
            {
                try
                {
                    Application.Quit();
                    Log.Info("WebConsole", "Application.Quit 已执行。");
                }
                catch (Exception ex)
                {
                    Log.Exception("WebConsole", ex, "退出游戏失败");
                }
            });
            HttpServer.WriteJson(ctx.Response, new { ok = true });
        }

        public static void HandleMeta(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, new { ok = true, wsPort = WebConsoleOptions.WsPort });
    }
}
