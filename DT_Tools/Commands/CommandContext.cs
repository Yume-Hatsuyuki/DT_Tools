using System;
using DT_Tools.Core;

namespace DT_Tools.Commands
{
    public sealed class CommandContext
    {
        private readonly string _tag;
        private readonly string _session;

        public string[] Args { get; }

        internal CommandContext(string tag, string[] args)
        {
            _tag = tag;
            Args = args ?? Array.Empty<string>();
            _session = CommandSession.Current;
        }

        public void Reply(string message) => Write(m => Log.Info(_tag, m), message);

        public void Warn(string message) => Write(m => Log.Warn(_tag, m), message);

        public CommandResult Defer()
        {
            var sink = DeferredCompletion.Current;
            return sink == null ? null : CommandResult.Defer(sink);
        }

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
