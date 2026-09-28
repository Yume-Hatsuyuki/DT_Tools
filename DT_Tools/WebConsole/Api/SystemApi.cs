using System;
using System.Net;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.WebConsole.Api
{
    /// <summary>
    /// /api/system/* — 桌面壳系统操作（WebUI 左下角菜单调用）。
    /// 与命令不同：这些操作作用于本机游戏进程本身，不属于任何命令域，故独立成 API。
    /// </summary>
    public static class SystemApi
    {
        /// <summary>
        /// POST /api/game/exit — 退出游戏。Application.Quit 必须在主线程调用（Unity API），
        /// 借 WebConsole 的主线程队列投递；游戏将走正常退出流程（OnDestroy → 保存）。
        /// </summary>
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
    }
}
