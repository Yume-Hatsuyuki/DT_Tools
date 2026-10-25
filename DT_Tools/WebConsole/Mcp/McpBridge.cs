using BepInEx.Logging;
using UnityEngine;

namespace DT_Tools.WebConsole.Mcp
{
    public sealed class McpBridge : MonoBehaviour
    {
        public static McpBridge Instance { get; private set; }

        private HttpServer _server;

        public static bool Running => Instance != null;

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
