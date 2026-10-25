using DT_Tools.Core.Attributes;

namespace DT_Tools.WebConsole
{
    [ConfigSection("内嵌 WebUI 控制台服务器。启动后用浏览器打开 http://<ListenIp>:<Port>/")]
    public sealed class WebConsoleOptions
    {
        [Config("Author: 梦初雪\n是否启用控制台 WebUI。")]
        public static bool Enabled = true;

        [Config("WebUI 监听端口。", Min = 1024, Max = 65535)]
        public static int Port = 19450;

        [Config("WebSocket 实时流独立端口（0=禁用实时流，前端回退轮询）。Unity 的 Mono 运行时不支持 HttpListener WebSocket 升级，实时流由本端口的自管 TCP 通道提供服务；前端经 /api/meta 自动发现端口，通常无需修改，仅防火墙需放行。", Min = 0, Max = 65535)]
        public static int WsPort = 19451;

        [Config("监听 IP：127.0.0.1=仅本机；0.0.0.0=所有 IPv4 网卡；::=所有 IPv6（通常双栈同收）；也可指定本机具体 IP（IPv4/IPv6 均可，IPv6 自动加方括号）。非回环地址会暴露给局域网，建议同时设置访问密码。")]
        public static string ListenIp = "127.0.0.1";

        [Config("反向代理使用的公网域名（逗号分隔，例如 dt.example.com）。填入后，这些域名的 Origin 与 Host 会被放行（HTTP 与 WebSocket 都生效）。留空=仅本机访问（默认）。nginx 默认的 proxy_set_header Host $proxy_host 可直接使用；若用 Host $host，需监听 0.0.0.0 等通配地址，否则监听层会先拒绝。修改后重启游戏生效。")]
        public static string AllowedHosts = "";

        [Config("访问密码。留空则不需要密码；设置后登录换取会话 token（cookie 不再存明文密码）。")]
        public static string Password = "";

        [Config("游戏窗口失焦时保持主线程运行（Unity 后台运行）。从其他设备访问 WebUI 时必须开启：否则游戏一失焦主线程就停摆，所有命令都会超时。")]
        public static bool RunInBackground = true;
    }
}
