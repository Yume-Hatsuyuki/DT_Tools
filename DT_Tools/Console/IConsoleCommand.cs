namespace DT_Tools.Console
{
    /// <summary>
    /// 控制台命令接口。
    /// 所有命令在 Unity 主线程执行，可安全访问游戏对象。
    /// </summary>
    internal interface IConsoleCommand
    {
        /// <summary>主命令名（不含 /），大小写不敏感。</summary>
        string Name { get; }

        /// <summary>别名列表，可为空数组。</summary>
        string[] Aliases { get; }

        /// <summary>在 /help 中显示的一行用法说明。</summary>
        string Usage { get; }

        /// <summary>功能描述。</summary>
        string Description { get; }

        /// <summary>命令作者署名。</summary>
        string Author { get; }

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="args">命令名之后的参数列表（已按空格拆分）。</param>
        /// <param name="console">输出日志用。</param>
        void Execute(string[] args, WebConsole console);
    }
}
