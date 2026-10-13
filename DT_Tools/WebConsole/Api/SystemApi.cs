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

        /// <summary>
        /// GET /api/meta — 运行时能力发现。前端据此得知 WebSocket 实时流的独立端口
        /// （Mono 的 HttpListener 不支持升级，实时流走 WsServer 自管 TCP 通道）；
        /// wsPort=0 表示实时流被禁用，前端直接走轮询。仅暴露端口号，无敏感信息，
        /// 仍统一过鉴权（Router 在分发前校验）。
        /// </summary>
        public static void HandleMeta(HttpListenerContext ctx)
            => HttpServer.WriteJson(ctx.Response, new { ok = true, wsPort = WebConsoleOptions.WsPort });
    }
}
