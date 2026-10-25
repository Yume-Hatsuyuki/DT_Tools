using System;
using System.Collections.Generic;

namespace DT_Tools.Game.WebTablet
{
    public static class WebTabletStore
    {
        public const int MaxLines = 1000;
        public const int MaxClues = 500;
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
        private static int _round = 1;

        public static void AddLine(WebTabletLine line, string dedupeKey = null)
        {
            lock (_sync)
            {
                if (dedupeKey != null && !TryAddKey($"line:{_round}:{dedupeKey}")) return;
                line.Round = _round;
                line.Seq = ++_seq;
                _lines.Add(line);
                while (_lines.Count > MaxLines) _lines.RemoveAt(0);
            }
        }

        public static bool AddClue(WebTabletClue clue)
        {
            lock (_sync)
            {
                clue.Round = _round;
                if (!TryAddKey($"clue:{_round}:{clue.Key}")) return false;
                _clues.Add(clue);
                while (_clues.Count > MaxClues) _clues.RemoveAt(0);
                return true;
            }
        }

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

        public static byte[] GetPhoto(int key)
        {
            lock (_sync)
            {
                return _photos.TryGetValue(key, out byte[] data) ? data : null;
            }
        }

        public static void SetState(string code, string label, string time)
        {
            lock (_sync)
            {
                if (code == _stateCode) return;
                _stateCode = code ?? "";
                if (_stateCode == "Lobby") _round++;
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

        public static void ClearChat()
        {
            lock (_sync)
            {
                ClearChatLocked();
                _epoch++;
            }
        }

        public static void ClearClues()
        {
            lock (_sync)
            {
                ClearCluesLocked();
            }
        }

        public static void Clear()
        {
            lock (_sync)
            {
                ClearChatLocked();
                ClearCluesLocked();
                _epoch++;
            }
        }

        public static void SetPhotoReadyTick(int readyTick)
        {
            lock (_sync)
            {
                _photoReadyTick = readyTick;
            }
        }

        public static int PhotoCooldownMs()
        {
            lock (_sync)
            {
                int left = unchecked(_photoReadyTick - Environment.TickCount);
                return left > 0 ? left : 0;
            }
        }

        public static void Reset()
        {
            lock (_sync)
            {
                Clear();
                _stateCode = "";
                _stateLabel = "";
                _photoReadyTick = 0;
                _round = 1;
            }
        }

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
