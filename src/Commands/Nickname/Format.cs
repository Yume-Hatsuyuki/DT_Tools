// 作用：格式化 /Nickname 命令的回复文本；不负责命令执行与注册。
namespace DT_Tools.Commands.Nickname
{
    internal static class NicknameFormat
    {
        public static object Result(int playerId, string from, string to)
            => new { playerId, from, to };
    }
}
