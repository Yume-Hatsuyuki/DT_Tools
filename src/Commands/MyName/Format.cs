// 作用：格式化 /MyName 命令的回复文本；不负责命令执行与注册。
namespace DT_Tools.Commands.MyName
{
    internal static class MyNameFormat
    {
        public static object Result(string from, string to)
            => new { from, to };
    }
}
