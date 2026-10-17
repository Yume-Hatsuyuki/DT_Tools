using System;
using System.Collections.Generic;

namespace DT_Tools.Game.WebTablet
{
    /// <summary>
    /// 平板电脑的内存存储：聊天流、线索表、照片缓存与当前阶段。
    /// </summary>
    /// <remarks>
    /// 写入发生在 Unity 主线程（Harmony 补丁），读取发生在 HTTP 线程，所有成员都经 _sync 加锁。
    /// 条目创建后不可变，快照只复制列表引用。本类不依赖 Unity，便于纯逻辑测试。
    /// 阶段切换只在 SetState 中去重：同一阶段重复上报不产生标记。
    /// </remarks>
    public static class WebTabletStore
    {
        /// <summary>聊天流保留的最大条数，超出从最旧的一端丢弃。</summary>
        public const int MaxLines = 1000;
        /// <summary>线索表保留的最大条数。</summary>
        public const int MaxClues = 500;
        /// <summary>照片缓存的最大张数。每张不超过 WebTabletText.MaxPhotoBytes。</summary>
        public const int MaxPhotos = 30;
        private const int MaxDedupeKeys = 5000;

        private static readonly object _sync = new object();
        private static readonly List<WebTabletLine> _lines = new List<WebTabletLine>();
        private static readonly List<WebTabletClue> _clues = new List<WebTabletClue>();
        private static readonly HashSet<string> _keys = new HashSet<string>();
        private static readonly Dictionary<int, byte[]> _photos = new Dictionary<int, byte[]>();
        private static readonly Queue<int> _photoOrder = new Queue<int>();
        private static long _seq;
        private static int _photoSeq;
        private static int _epoch = 1;
        private static string _stateCode = "";
        private static string _stateLabel = "";
        private static int _photoReadyTick;

        /// <summary>
        /// 追加一条聊天条目。传入 dedupeKey 时，同一键只会入库一次（用于命题气泡可能被重复上报的情况）。
        /// </summary>
        /// <param name="line">要追加的条目；Seq 由本方法分配。</param>
        /// <param name="dedupeKey">去重键，可为 null 表示不去重。</param>
        public static void AddLine(WebTabletLine line, string dedupeKey = null)
        {
            lock (_sync)
            {
                if (dedupeKey != null && !TryAddKey($"line:{dedupeKey}")) return;
                line.Seq = ++_seq;
                _lines.Add(line);
                while (_lines.Count > MaxLines) _lines.RemoveAt(0);
            }
        }

        /// <summary>
        /// 追加一条线索。同一 Key 只入库一次。
        /// </summary>
        /// <param name="clue">线索条目，Key 必须已填写。</param>
        /// <returns>true 表示新入库；false 表示已存在被忽略。</returns>
        public static bool AddClue(WebTabletClue clue)
        {
            lock (_sync)
            {
                if (!TryAddKey($"clue:{clue.Key}")) return false;
                _clues.Add(clue);
                while (_clues.Count > MaxClues) _clues.RemoveAt(0);
                return true;
            }
        }

        /// <summary>
        /// 保存一张照片，返回其存储键。超过 MaxPhotos 时淘汰最早的一张。
        /// </summary>
        /// <param name="jpeg">JPEG 字节，调用方保证非空。</param>
        /// <returns>照片存储键，供 GetPhoto 与条目的 PhotoKey 使用。</returns>
        public static int AddPhoto(byte[] jpeg)
        {
            lock (_sync)
            {
                int key = ++_photoSeq;
                _photos[key] = jpeg;
                _photoOrder.Enqueue(key);
                while (_photoOrder.Count > MaxPhotos) _photos.Remove(_photoOrder.Dequeue());
                return key;
            }
        }

        /// <summary>
        /// 按存储键取照片字节。
        /// </summary>
        /// <param name="key">AddPhoto 返回的键。</param>
        /// <returns>照片字节；已被淘汰或不存在时返回 null。</returns>
        public static byte[] GetPhoto(int key)
        {
            lock (_sync)
            {
                return _photos.TryGetValue(key, out byte[] data) ? data : null;
            }
        }

        /// <summary>
        /// 记录阶段。同一 code 重复上报直接忽略；label 为空（如 NoneState）时只更新代码不产生标记。
        /// </summary>
        /// <param name="code">EGameState 枚举名。</param>
        /// <param name="label">阶段中文名，用于标记文本「进入「label」」。</param>
        /// <param name="time">标记的本机时间，HH:mm:ss。</param>
        public static void SetState(string code, string label, string time)
        {
            lock (_sync)
            {
                if (code == _stateCode) return;
                _stateCode = code ?? "";
                _stateLabel = label ?? "";
                if (_stateLabel.Length == 0) return;
                AddLine(new WebTabletLine
                {
                    Kind = WebTabletKind.State,
                    Time = time,
                    Text = $"进入「{_stateLabel}」",
                });
            }
        }

        /// <summary>
        /// 清空聊天流与照片，并递增 Epoch（前端据此丢弃本地聊天列表）。线索与阶段保留。
        /// </summary>
        public static void ClearChat()
        {
            lock (_sync)
            {
                ClearChatLocked();
                _epoch++;
            }
        }

        /// <summary>
        /// 清空线索表。聊天与阶段保留，Epoch 不变。
        /// </summary>
        public static void ClearClues()
        {
            lock (_sync)
            {
                ClearCluesLocked();
            }
        }

        /// <summary>
        /// 清空聊天、线索与照片，并递增 Epoch。阶段信息保留，避免清理后重复出现当前阶段标记。
        /// </summary>
        public static void Clear()
        {
            lock (_sync)
            {
                ClearChatLocked();
                ClearCluesLocked();
                _epoch++;
            }
        }

        /// <summary>
        /// 记录照片冷却的截止时刻（Environment.TickCount 基准）。由主线程在照片预算被消耗后写入。
        /// </summary>
        /// <param name="readyTick">冷却结束的 TickCount。</param>
        public static void SetPhotoReadyTick(int readyTick)
        {
            lock (_sync)
            {
                _photoReadyTick = readyTick;
            }
        }

        /// <summary>
        /// 距离下一次可发照片还需的毫秒数，0 表示可以发送。
        /// </summary>
        /// <returns>剩余冷却毫秒数，已结束时为 0。</returns>
        public static int PhotoCooldownMs()
        {
            lock (_sync)
            {
                int left = unchecked(_photoReadyTick - Environment.TickCount);
                return left > 0 ? left : 0;
            }
        }

        /// <summary>
        /// 功能关闭时调用：清空全部内容，并忘记阶段，下次启用时由 WebTabletBridge.SyncState 重新同步。
        /// </summary>
        public static void Reset()
        {
            lock (_sync)
            {
                Clear();
                _stateCode = "";
                _stateLabel = "";
                _photoReadyTick = 0;
            }
        }

        /// <summary>
        /// 取快照：版本号、当前阶段、序号大于 since 的聊天条目，以及全部线索。
        /// </summary>
        /// <param name="since">调用方已收到的最大序号；0 表示全量。</param>
        /// <returns>快照对象，列表为副本，可在锁外使用。</returns>
        public static WebTabletSnapshot Snapshot(long since)
        {
            lock (_sync)
            {
                var lines = new List<WebTabletLine>();
                for (int i = _lines.Count - 1; i >= 0 && _lines[i].Seq > since; i--)
                    lines.Add(_lines[i]);
                lines.Reverse();
                return new WebTabletSnapshot
                {
                    Epoch = _epoch,
                    Seq = _seq,
                    StateCode = _stateCode,
                    StateLabel = _stateLabel,
                    Lines = lines,
                    Clues = new List<WebTabletClue>(_clues),
                };
            }
        }

        private static void ClearChatLocked()
        {
            _lines.Clear();
            _photos.Clear();
            _photoOrder.Clear();
            _keys.RemoveWhere(k => k.StartsWith("line:", StringComparison.Ordinal));
        }

        private static void ClearCluesLocked()
        {
            _clues.Clear();
            _keys.RemoveWhere(k => k.StartsWith("clue:", StringComparison.Ordinal));
        }

        private static bool TryAddKey(string key)
        {
            if (_keys.Count >= MaxDedupeKeys) _keys.Clear();
            return _keys.Add(key);
        }
    }
}
