using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DT_Tools.Core;

namespace DT_Tools
{
    /// <summary>
    /// 全项目唯一日志入口。Info/Warn/Error/Fatal/Debug 同时写三处：BepInEx 主日志、
    /// 全局环形缓冲（WebUI 控制台页）、按 tag 的段级环形缓冲（WebUI 配置/自动化页）。
    /// 例外：Exception() 的完整堆栈只进 BepInEx（LogOutput.log 可查），环形缓冲只留单行摘要，
    /// 避免大文本刷掉常用条目。
    /// tag 一律用段名：Log.Info&lt;TFeature&gt;(…) 自动取 Engine.SectionOf&lt;T&gt;()。
    ///
    /// 注意：本类必须放在 DT_Tools 根命名空间——游戏在全局命名空间有一个 public static class
    /// Log（0.1.15b Log.cs），C# 命名空间查找链上 DT_Tools.Log 优先于它，否则会被遮蔽。
    /// </summary>
    public static class Log
    {
        private const int GlobalCapacity = 200;
        private const int TagCapacity = 100;

        private static ManualLogSource _src;
        private static readonly object Gate = new object();
        private static readonly RingBuffer<LogEntry> GlobalRing = new RingBuffer<LogEntry>(GlobalCapacity);
        private static readonly Dictionary<string, RingBuffer<LogEntry>> TagRings =
            new Dictionary<string, RingBuffer<LogEntry>>(StringComparer.OrdinalIgnoreCase);
        private static long _seq;

        public static void Init(ManualLogSource source) => _src = source;

        // ---- 面向人的写入口（tag 显式给出）----

        /// <summary>详细诊断：进入环形缓冲（WebUI 白色），不要用它输出异常堆栈——用 <see cref="Exception"/>。</summary>
        public static void Debug(string tag, string message)
        {
            _src?.LogDebug(Format(tag, message));
            Append("DEBUG", tag, message);
        }

        public static void Info(string tag, string message) => Write("INFO", tag, message);
        public static void Warn(string tag, string message) => Write("WARN", tag, message);
        public static void Error(string tag, string message) => Write("ERROR", tag, message);

        /// <summary>致命错误（功能无法继续 / 数据损坏级）：BepInEx LogFatal，WebUI 红色。</summary>
        public static void Fatal(string tag, string message) => Write("FATAL", tag, message);

        /// <summary>异常：BepInEx 进完整堆栈（含内层异常），环形缓冲只留「类型: 消息」单行摘要。</summary>
        public static void Exception(string tag, Exception ex, string context = null)
        {
            if (ex == null) return;
            string summary = context == null
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{context}：{ex.GetType().Name}: {ex.Message}";
            _src?.LogError(Format(tag, context == null ? ex.ToString() : $"{context}：{ex}"));
            Append("ERROR", tag, summary);
        }

        /// <summary>断言：condition 为 false 时按 Error 记录（对齐 Unity Debug.Assert 语义，开发期自检用）。</summary>
        public static void Assert(string tag, bool condition, string message)
        {
            if (condition) return;
            Write("ERROR", tag, "[Assert] " + message);
        }

        // ---- tag = SectionOf<T>() 的便捷重载 ----

        public static void Debug<T>(string message) => Debug(Engine.SectionOf<T>(), message);
        public static void Info<T>(string message) => Info(Engine.SectionOf<T>(), message);
        public static void Warn<T>(string message) => Warn(Engine.SectionOf<T>(), message);
        public static void Error<T>(string message) => Error(Engine.SectionOf<T>(), message);
        public static void Fatal<T>(string message) => Fatal(Engine.SectionOf<T>(), message);
        public static void Exception<T>(Exception ex, string context = null) => Exception(Engine.SectionOf<T>(), ex, context);
        public static void Assert<T>(bool condition, string message) => Assert(Engine.SectionOf<T>(), condition, message);

        // ---- 环形缓冲快照（WebUI 日志 API 用）----

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

        // ---- 内部 ----

        /// <summary>BepInEx + 全局缓冲 + 段缓冲三写（Debug 走 <see cref="Debug"/>，Exception 走 <see cref="Exception"/>）。</summary>
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

        /// <summary>只写两处环形缓冲（已写过 BepInEx 时复用；持锁内构造条目保证 Seq 单调）。</summary>
        private static void Append(string level, string tag, string message)
        {
            lock (Gate)
            {
                _seq++;
                var entry = new LogEntry(_seq, DateTime.Now, level, tag ?? "", message ?? "");
                GlobalRing.Append(entry);
                if (string.IsNullOrEmpty(tag)) return;
                if (!TagRings.TryGetValue(tag, out var ring))
                    TagRings[tag] = ring = new RingBuffer<LogEntry>(TagCapacity);
                ring.Append(entry);
            }
        }

        private static string Format(string tag, string message) => $"[{tag}] {message}";
    }
}
