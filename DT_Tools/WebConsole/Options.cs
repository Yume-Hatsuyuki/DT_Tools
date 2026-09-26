using DT_Tools.Core.Attributes;

namespace DT_Tools.WebConsole
{
    /// <summary>WebConsole 服务器配置（段名自动推导为 "WebConsole"）。</summary>
    [ConfigSection("内嵌 WebUI 控制台服务器。启动后用浏览器打开 http://127.0.0.1:<Port>/")]
    public sealed class WebConsoleOptions
    {
        [Config("Author: 梦初雪\n是否启用控制台 WebUI。")]
        public static bool Enabled = true;

        [Config("WebUI 监听端口（仅回环地址）。", Min = 1024, Max = 65535)]
        public static int Port = 19450;

        [Config("访问密码。留空则不需要密码；设置后登录换取会话 token（cookie 不再存明文密码）。")]
        public static string Password = "";
    }
}
