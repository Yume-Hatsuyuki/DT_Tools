namespace DT_Tools.Console
{
    /// <summary>
    /// 控制台命令。主线程执行。
    /// </summary>
    internal interface IConsoleCommand
    {
        string Name { get; }
        string[] Aliases { get; }
        string Usage { get; }
        string Description { get; }
        string Author { get; }

        /// <summary>true 时要求当前为房主，否则拒绝执行。</summary>
        bool RequireHost => false;

        void Execute(string[] args, WebConsole console);
    }
}
