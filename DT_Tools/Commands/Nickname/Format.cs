namespace DT_Tools.Commands.Nickname
{
    internal static class NicknameFormat
    {
        public static object Result(int playerId, string from, string to)
            => new { playerId, from, to };
    }
}
