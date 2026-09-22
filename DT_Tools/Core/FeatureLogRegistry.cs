using System;
using System.Collections.Generic;

namespace DT_Tools.Core
{
    /// <summary>
    /// 按配置段名存放的功能日志（供 DT CONFIG 可折叠控制台）。
    /// 补丁可调用 FeatureLogRegistry.Info(section, msg)；未写入时前端显示空。
    /// </summary>
    internal static class FeatureLogRegistry
    {
        private const int Capacity = 100;
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Ring> Rings =
            new Dictionary<string, Ring>(StringComparer.OrdinalIgnoreCase);

        public static void Info(string section, string message) => Append(section, "INFO", message);
        public static void Warn(string section, string message) => Append(section, "WARN", message);
        public static void Error(string section, string message) => Append(section, "ERROR", message);

        public static void Append(string section, string level, string message)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(message)) return;
            string line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
            lock (Gate)
            {
                if (!Rings.TryGetValue(section, out var ring))
                {
                    ring = new Ring(Capacity);
                    Rings[section] = ring;
                }
                ring.Add(line);
            }
        }

        public static void Clear(string section)
        {
            if (string.IsNullOrEmpty(section)) return;
            lock (Gate)
            {
                if (Rings.TryGetValue(section, out var ring))
                    ring.Clear();
            }
        }

        public static (long seq, string[] lines) Snapshot(string section)
        {
            lock (Gate)
            {
                if (!Rings.TryGetValue(section, out var ring))
                    return (0, Array.Empty<string>());
                return (ring.Seq, ring.ToArray());
            }
        }

        private sealed class Ring
        {
            private readonly string[] _buf;
            private int _start;
            private int _count;
            public long Seq;

            public Ring(int capacity) => _buf = new string[capacity];

            public void Add(string line)
            {
                int idx;
                if (_count == _buf.Length)
                {
                    _start = (_start + 1) % _buf.Length;
                    idx = (_start + _count - 1) % _buf.Length;
                }
                else
                {
                    idx = (_start + _count) % _buf.Length;
                    _count++;
                }
                _buf[idx] = line;
                Seq++;
            }

            public void Clear()
            {
                _start = 0;
                _count = 0;
                Array.Clear(_buf, 0, _buf.Length);
                Seq++;
            }

            public string[] ToArray()
            {
                var a = new string[_count];
                for (int i = 0; i < _count; i++)
                    a[i] = _buf[(_start + i) % _buf.Length];
                return a;
            }
        }
    }
}
