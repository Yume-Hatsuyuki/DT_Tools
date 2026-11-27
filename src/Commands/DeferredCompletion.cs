// 作用：管理延迟完成的命令会话；不负责命令注册与业务实现。
using System;
using System.Threading;

namespace DT_Tools.Commands
{
    public static class DeferredCompletion
    {
        [ThreadStatic] private static Action<CommandResult> _current;

        public static Action<CommandResult> Current => _current;

        public static void Begin(Action<CommandResult> sink) => _current = sink;

        public static void End() => _current = null;
    }
}
