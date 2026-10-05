using DT_Tools.Core.Attributes;

namespace DT_Tools.WebConsole.Mcp
{
    /// <summary>
    /// MCP 桥接配置（段名自动推导为 "Mcp"）。独立监听，与 WebConsole 互不依赖。
    /// 两处配置同一份：WebUI 的 MCP 挂件与「DT 配置」页都经 ConfigService 读写本段，天然同步。
    /// Port/ListenIp/Password 热改不重绑监听（与 WebConsole 同口径），重启游戏生效。
    /// </summary>
    [ConfigSection("MCP 桥接服务器（独立监听）。任何支持 MCP Streamable HTTP 的客户端经 http://<ListenIp>:<Port>/mcp 连接，协议包语义见 .github/skills/MCP/。")]
    public sealed class McpOptions
    {
        [Config("Author: 梦初雪\n是否启用 MCP 桥接。启动时启用则拉起独立监听；运行期关闭时 /mcp 立即返回 503（工具面停用），重新打开即时生效。")]
        public static bool Enabled = true;

        [Config("MCP 监听端口（独立于 WebConsole 的 19450）。修改后重启游戏生效。", Min = 1024, Max = 65535)]
        public static int Port = 19452;

        [Config("监听 IP：127.0.0.1=仅本机；0.0.0.0=所有 IPv4 网卡；::=所有 IPv6（自动补注册一条 IPv4 前缀保证局域网可达，Mono 解析不了方括号 IPv6 的 Host 头）；也可指定本机具体 IP（IPv4/IPv6 均可，IPv6 自动加方括号并补回环）。非回环地址会暴露给局域网，建议同时设置访问密码。修改后重启游戏生效。")]
        public static string ListenIp = "127.0.0.1";

        [Config("访问密码。留空则不需要密码；MCP 客户端以请求头 Authorization: Bearer <密码> 接入。修改后重启游戏生效。")]
        public static string Password = "";

        [Config("Author: 梦初雪\n把每次工具调用写进全局日志（调用：工具名+参数；结果：ok/fail+耗时），WebUI 日志页与 AI 的 read_log 都可见，便于人工核对 AI 的每一步操作。")]
        public static bool LogCalls = true;

        [Config("游戏窗口失焦时保持主线程运行（Unity 后台运行）。AI 远程/后台操作游戏时必须开启：否则游戏一失焦主线程就停摆，所有工具调用都会超时。")]
        public static bool RunInBackground = true;
    }
}
