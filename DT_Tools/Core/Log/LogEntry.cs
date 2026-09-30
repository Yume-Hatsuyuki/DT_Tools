using System;

namespace DT_Tools.Core
{
    /// <summary>结构化日志条目（存于环形缓冲，供 WebUI 日志 API 按字段输出）。</summary>
    public readonly struct LogEntry
    {
        public readonly long Seq;
        public readonly DateTime Time;
        public readonly string Level;
        public readonly string Tag;
        public readonly string Message;

        /// <summary>
        /// 发出该条目的 WebUI 命令会话 id（/api/run 头 X-DT-Session）；null=非命令上下文
        /// （全局日志）。前端据此做控制台会话隔离——控制台只显示自己会话的命令输出，
        /// 全量日志归日志应用。
        /// </summary>
        public readonly string Session;

        public LogEntry(long seq, DateTime time, string level, string tag, string message, string session = null)
        {
            Seq = seq;
            Time = time;
            Level = level;
            Tag = tag ?? "";
            Message = message ?? "";
            Session = session;
        }
    }
}
