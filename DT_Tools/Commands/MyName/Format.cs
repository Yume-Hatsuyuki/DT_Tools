namespace DT_Tools.Commands.MyName
{
    /// /myname 输出 DTO。
    internal static class MyNameFormat
    {
        public static object Result(string from, string to)
            => new { from, to };
    }
}
