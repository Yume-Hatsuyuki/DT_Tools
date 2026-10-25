using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DT_Tools.Core;

namespace DT_Tools
{
    public static class Log
    {
        private const int GlobalCapacity = 2000;
        private const int TagCapacity = 100;

        private static ManualLogSource _src;
        private static readonly object Gate = new object();
        private static readonly RingBuffer<LogEntry> GlobalRing = new RingBuffer<LogEntry>(GlobalCapacity);
        private static readonly Dictionary<string, RingBuffer<LogEntry>> TagRings =
            new Dictionary<string, RingBuffer<LogEntry>>(StringComparer.OrdinalIgnoreCase);
        private static long _seq;

        public static event Action<LogEntry> EntryAppended;

        public static void Init(ManualLogSource source) => _src = source;

        public static void Debug(string tag, string message)
        {
            _src?.LogDebug(Format(tag, message));
            Append("DEBUG", tag, message);
        }

        public static void Info(string tag, string message) => Write("INFO", tag, message);
        public static void Warn(string tag, string message) => Write("WARN", tag, message);
        public static void Error(string tag, string message) => Write("ERROR", tag, message);

        public static void Warn(string tag, Exception ex, string context) => Write("WARN", tag, Failure(ex, context));
        public static void Error(string tag, Exception ex, string context) => Write("ERROR", tag, Failure(ex, context));
        public static void Warn<T>(Exception ex, string context) => Warn(Engine.SectionOf<T>(), ex, context);
        public static void Error<T>(Exception ex, string context) => Error(Engine.SectionOf<T>(), ex, context);
        private static string Failure(Exception ex, string context) => $"{context}：{ex?.Message}";

        public static void Fatal(string tag, string message) => Write("FATAL", tag, message);

        public static void Exception(string tag, Exception ex, string context = null)
        {
            if (ex == null) return;
            string summary = context == null
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{context}：{ex.GetType().Name}: {ex.Message}";
            _src?.LogError(Format(tag, context == null ? ex.ToString() : $"{context}：{ex}"));
            Append("ERROR", tag, summary);
        }

        public static void Debug<T>(string message) => Debug(Engine.SectionOf<T>(), message);
        public static void Info<T>(string message) => Info(Engine.SectionOf<T>(), message);
        public static void Warn<T>(string message) => Warn(Engine.SectionOf<T>(), message);
        public static void Error<T>(string message) => Error(Engine.SectionOf<T>(), message);
        public static void Fatal<T>(string message) => Fatal(Engine.SectionOf<T>(), message);
        public static void Exception<T>(Exception ex, string context = null) => Exception(Engine.SectionOf<T>(), ex, context);

        public static (long seq, LogEntry[] entries) SnapshotEntries() => GlobalRing.Snapshot();

        public static (long seq, LogEntry[] entries) SnapshotEntries(string tag)
        {
            lock (Gate)
            {
                return TagRings.TryGetValue(tag, out var ring)
                    ? ring.Snapshot()
                    : (0, Array.Empty<LogEntry>());
            }
        }

        public static void Clear(string tag)
        {
            lock (Gate)
            {
                if (TagRings.TryGetValue(tag, out var ring))
                    ring.Clear();
            }
        }

        private static void Write(string level, string tag, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            tag ??= "";
            string formatted = $"[{tag}] {message}";

            switch (level)
            {
                case "WARN": _src?.LogWarning(formatted); break;
                case "ERROR": _src?.LogError(formatted); break;
                case "FATAL": _src?.LogFatal(formatted); break;
                case "DEBUG": _src?.LogDebug(formatted); break;
                default: _src?.LogInfo(formatted); break;
            }

            Append(level, tag, message);
        }

        private static void Append(string level, string tag, string message)
        {
            LogEntry entry;
            lock (Gate)
            {
                _seq++;
                entry = new LogEntry(_seq, DateTime.Now, level, tag ?? "", message ?? "", CommandSession.Current);
                GlobalRing.Append(entry);
                if (!string.IsNullOrEmpty(tag))
                {
                    if (!TagRings.TryGetValue(tag, out var ring))
                        TagRings[tag] = ring = new RingBuffer<LogEntry>(TagCapacity);
                    ring.Append(entry);
                }
            }
            EntryAppended?.Invoke(entry);
        }

        private static string Format(string tag, string message) => $"[{tag}] {message}";
    }
}
