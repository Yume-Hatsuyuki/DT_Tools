namespace DT_Tools.Commands.Nickname
{
    /// /nick 输出 DTO。
    internal static class NicknameFormat
    {
        public static object Result(int playerId, string from, string to)
            => new { playerId, from, to };
    }
}
