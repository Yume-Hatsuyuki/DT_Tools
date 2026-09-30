namespace DT_Tools.Commands.MyName
{
    /// <summary>/myname 输出 DTO。</summary>
    internal static class MyNameFormat
    {
        public static object Result(string from, string to)
            => new { from, to };
    }
}
