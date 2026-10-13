using System;

namespace DT_Tools.Core
{
    /// <summary>
    /// 命令执行会话标记（环境量）：WebConsole 的主线程泵执行 /api/run 命令前
    /// <see cref="Begin"/>，执行完 <see cref="End"/>；期间 <see cref="Log"/> 写出的条目
    /// 携带 Session，前端按会话隔离各控制台的显示（控制台只看自己发的命令产出）。
    /// ThreadStatic：只有执行命令的主线程携带标记——HTTP 线程日志、自动化 Tick、
    /// 游戏回调都不受影响（Session=null → 全局日志，归日志应用）。
    /// </summary>
    public static class CommandSession
    {
        [ThreadStatic] private static string _current;

        public static string Current => _current;

        public static void Begin(string sessionId) => _current = sessionId;

        public static void End() => _current = null;
    }
}
