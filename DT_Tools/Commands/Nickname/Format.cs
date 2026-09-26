namespace DT_Tools.Commands.Nickname
{
    /// <summary>/nick 输出 DTO。</summary>
    internal static class NicknameFormat
    {
        public static object Result(int playerId, string from, string to)
            => new { playerId, from, to };
    }
}
