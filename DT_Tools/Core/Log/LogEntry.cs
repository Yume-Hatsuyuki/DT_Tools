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

        public LogEntry(long seq, DateTime time, string level, string tag, string message)
        {
            Seq = seq;
            Time = time;
            Level = level;
            Tag = tag ?? "";
            Message = message ?? "";
        }
    }
}
