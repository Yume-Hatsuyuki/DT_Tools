// 作用：记录当前线程的命令会话标识；不提供命令注册。
using System;

namespace DT_Tools.Core
{
    public static class CommandSession
    {
        [ThreadStatic] private static string _current;

        public static string Current => _current;

        public static void Begin(string sessionId) => _current = sessionId;

        public static void End() => _current = null;
    }
}
