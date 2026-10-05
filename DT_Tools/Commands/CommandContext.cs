using System;
using DT_Tools.Core;

namespace DT_Tools.Commands
{
    /// <summary>
    /// 命令执行上下文：原始参数 + 人类可读回复通道（进统一日志，WebUI 控制台页可见）。
    /// 会话随行：构造时快照当前命令会话（<see cref="CommandSession"/> 的 ThreadStatic
    /// 标记只活在 WebConsole 执行命令的那一段同步代码里），Reply/Warn 写日志时若标记
    /// 已不在——异步命令在 Steam 回调、周期 ticker 等 Execute 返回之后的场合回复
    /// （如 RoomList、Agent）——临时恢复快照再写、写完还原。异步回显因此能带着会话
    /// 流回发起命令的控制台窗口，同时照常进全局环形缓冲（日志应用可见）；
    /// 否则条目落成无会话的全局日志，控制台永远收不到。
    /// </summary>
    public sealed class CommandContext
    {
        private readonly string _tag;
        private readonly string _session;

        /// <summary>原始参数（空格分词后，不含命令名本身）。</summary>
        public string[] Args { get; }

        internal CommandContext(string tag, string[] args)
        {
            _tag = tag;
            Args = args ?? Array.Empty<string>();
            _session = CommandSession.Current;
        }

        /// <summary>普通回复。</summary>
        public void Reply(string message) => Write(m => Log.Info(_tag, m), message);

        /// <summary>警告回复。</summary>
        public void Warn(string message) => Write(m => Log.Warn(_tag, m), message);

        /// <summary>
        /// 延迟完成：Execute 返回本结果（占位），异步收尾（主线程）把真实结果经返回值的
        /// <see cref="CommandResult.Complete"/> 回填，等待命令结果的通道（WebUI /api/run、
        /// MCP run_command）随即拿到终值——用于"结果必须晚于本帧"的命令（如 shot 的帧末
        /// 捕获）。当前通道不支持等待时返回 null，调用方应退化为只发起 + 日志查询。
        /// </summary>
        public CommandResult Defer()
        {
            var sink = DeferredCompletion.Current;
            return sink == null ? null : CommandResult.Defer(sink);
        }

        /// <summary>同步路径上标记本来就一致，直接写；不一致则按快照恢复标记，写完还原
        /// （还原而非清空：回调若恰好插在另一命令的同步执行段中，不破坏对方的标记）。</summary>
        private void Write(Action<string> write, string message)
        {
            if (CommandSession.Current == _session)
            {
                write(message);
                return;
            }
            string prev = CommandSession.Current;
            CommandSession.Begin(_session);
            try
            {
                write(message);
            }
            finally
            {
                CommandSession.Begin(prev);
            }
        }
    }
}
