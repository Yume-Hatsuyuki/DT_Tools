using System;
using DT_Tools.Core;

namespace DT_Tools.Commands
{
    /// <summary>命令执行上下文：原始参数 + 人类可读回复通道（进统一日志，WebUI 控制台页可见）。</summary>
    public sealed class CommandContext
    {
        private readonly string _tag;

        /// <summary>原始参数（空格分词后，不含命令名本身）。</summary>
        public string[] Args { get; }

        internal CommandContext(string tag, string[] args)
        {
            _tag = tag;
            Args = args ?? Array.Empty<string>();
        }

        /// <summary>普通回复。</summary>
        public void Reply(string message) => Log.Info(_tag, message);

        /// <summary>警告回复。</summary>
        public void Warn(string message) => Log.Warn(_tag, message);
    }
}
