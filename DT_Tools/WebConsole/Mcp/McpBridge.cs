using BepInEx.Logging;
using UnityEngine;

namespace DT_Tools.WebConsole.Mcp
{
    /// <summary>
    /// MCP 独立监听组件：装配（Auth/Router/HttpServer）并持有生命周期。
    /// 与 WebConsole 平级但互不依赖——WebConsole 停用时 MCP 照常工作；传输层复用
    /// WebConsole 的 HttpServer/Router/Auth。/mcp 与 status/toggle 同时注册在两条路由面
    /// （本监听 + WebConsole 路由），共用同一套静态处理器，MCP 客户端走哪个门都等价。
    /// 线程模型同 WebConsole：监听线程 + ThreadPool 派发；工具触 Unity API 自走 RunOnMain。
    /// </summary>
    public sealed class McpBridge : MonoBehaviour
    {
        public static McpBridge Instance { get; private set; }

        private HttpServer _server;

        /// <summary>监听是否已拉起（status 展示用：启动时已停用则本监听不在，仅 WebConsole 门可用）。</summary>
        public static bool Running => Instance != null;

        /// <summary>由 Plugin 在 McpOptions.Enabled 时调用。</summary>
        public void Init(ManualLogSource log)
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var auth = new Auth(McpOptions.Password);
            var router = new Router(auth, McpOptions.ListenIp, serveStaticFiles: false);
            McpServer.RegisterRoutes(router);

            _server = new HttpServer(router, "DT_Mcp");
            _server.Start(McpOptions.ListenIp, McpOptions.Port);

            // 与 WebConsole 同口径的运行前置：失焦不停摆 + 暴露面警告
            if (McpOptions.RunInBackground && !Application.runInBackground)
            {
                Application.runInBackground = true;
                Log.Info("Mcp", "已开启 Unity 后台运行（RunInBackground）：游戏窗口失焦时 MCP 工具仍会执行。");
            }
            string ip = (McpOptions.ListenIp ?? "").Trim();
            bool loopback = ip == "127.0.0.1" || ip == "::1" || ip == "localhost";
            if (!loopback && string.IsNullOrEmpty(McpOptions.Password))
                Log.Warn("Mcp", "监听地址为非回环 IP 且未设置访问密码——局域网内任何人都可以操作 MCP 工具，建议在配置中设置 Password。");
        }

        private void OnDestroy()
        {
            _server?.Stop();
            if (Instance == this) Instance = null;
        }
    }
}
