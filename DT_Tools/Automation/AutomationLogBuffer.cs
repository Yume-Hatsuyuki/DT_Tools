using System;
using System.Collections.Generic;
using System.Text;

namespace DT_Tools.Automation
{
    /// <summary>
    /// 单模块环形日志；供 WEBUI 卡片独立控制台使用。
    /// </summary>
    internal sealed class AutomationLogBuffer
    {
        public const int DefaultCapacity = 120;

        private readonly object _lock = new object();
        private readonly string[] _lines;
        private int _start;
        private int _count;
        private long _seq;

        public AutomationLogBuffer(int capacity = DefaultCapacity)
        {
            if (capacity < 8) capacity = 8;
            _lines = new string[capacity];
        }

        public void Info(string message) => Append("INFO", message);
        public void Warn(string message) => Append("WARN", message);
        public void Error(string message) => Append("ERROR", message);

        public void Append(string level, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            string line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
            lock (_lock)
            {
                int idx = (_start + _count) % _lines.Length;
                if (_count == _lines.Length)
                {
                    _start = (_start + 1) % _lines.Length;
                    idx = (_start + _count - 1) % _lines.Length;
                }
                else
                {
                    _count++;
                }
                _lines[idx] = line;
                _seq++;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _start = 0;
                _count = 0;
                Array.Clear(_lines, 0, _lines.Length);
                _seq++;
            }
        }

        public long Seq
        {
            get { lock (_lock) return _seq; }
        }

        public string[] Snapshot()
        {
            lock (_lock)
            {
                var arr = new string[_count];
                for (int i = 0; i < _count; i++)
                    arr[i] = _lines[(_start + i) % _lines.Length];
                return arr;
            }
        }

        public string SnapshotText()
        {
            var lines = Snapshot();
            if (lines.Length == 0) return "";
            var sb = new StringBuilder(lines.Length * 48);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(lines[i]);
            }
            return sb.ToString();
        }
    }
}
