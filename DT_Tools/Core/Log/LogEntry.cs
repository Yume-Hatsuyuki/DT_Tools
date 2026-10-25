using System;

namespace DT_Tools.Core
{
    public readonly struct LogEntry
    {
        public readonly long Seq;
        public readonly DateTime Time;
        public readonly string Level;
        public readonly string Tag;
        public readonly string Message;

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
