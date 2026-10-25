using System;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Game
{
    public static class LobbySpawns
    {
        public const float GridScale = 224f;

        private static readonly Dictionary<string, ERoomType> _targets = new Dictionary<string, ERoomType>();

        public static void Set(string accountId, ERoomType room) => _targets[accountId] = room;

        public static bool Remove(string accountId) => _targets.Remove(accountId);

        public static bool TryGet(string accountId, out ERoomType room)
        {
            if (!string.IsNullOrEmpty(accountId))
                return _targets.TryGetValue(accountId, out room);
            room = default;
            return false;
        }

        public static void Clear() => _targets.Clear();

        public static IReadOnlyDictionary<string, ERoomType> Snapshot() => new Dictionary<string, ERoomType>(_targets);

        public static (float X, float Y) CellCenter(int cellX, int cellY, int offsetX, int offsetY) =>
            ((cellX + offsetX + 0.5f) * GridScale, (cellY + offsetY + 0.5f) * GridScale);

        public static (int X, int Y) PickRoomCell(IReadOnlyList<(int X, int Y)> cells)
        {
            if (cells == null || cells.Count == 0)
                throw new ArgumentException("房间没有格子", nameof(cells));

            double cx = cells.Average(c => c.X);
            double cy = cells.Average(c => c.Y);
            (int X, int Y) best = cells[0];
            double bestDist = double.MaxValue;
            foreach (var c in cells)
            {
                double dx = c.X - cx;
                double dy = c.Y - cy;
                double d = dx * dx + dy * dy;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        public static bool TryFindRoomCenter(ERoomType room, out PosInfo pos)
        {
            pos = null;
            var data = Managers.Data;
            var map = data?.MapData;
            var grid = data?.MapArray;
            if (map == null || grid == null)
                return false;

            var cells = new List<(int X, int Y)>();
            byte target = (byte)room;
            for (int x = 0; x < grid.Count; x++)
            {
                List<byte> column = grid[x];
                for (int y = 0; y < column.Count; y++)
                {
                    if (column[y] == target)
                        cells.Add((x, y));
                }
            }
            if (cells.Count == 0)
                return false;

            var cell = PickRoomCell(cells);
            var (cx, cy) = CellCenter(cell.X, cell.Y, (int)map.MapOffset.X, (int)map.MapOffset.Y);
            pos = new PosInfo { X = cx, Y = cy };
            return true;
        }

        public static int CellIndex(float coordinate, int mapOffset) => (int)(coordinate / GridScale) - mapOffset;

        public static bool IsWalkableCell(int cellX, int cellY, int width, int height, byte value) =>
            cellX >= 0 && cellY >= 0 && cellX < width && cellY < height && value != 0;

        public static bool TryResolveCustom(float x, float y, out PosInfo pos)
        {
            pos = null;
            var data = Managers.Data;
            var map = data?.MapData;
            var grid = data?.MapArray;
            if (map == null || grid == null)
                return false;

            int cellX = CellIndex(x, (int)map.MapOffset.X);
            int cellY = CellIndex(y, (int)map.MapOffset.Y);
            if (cellX < 0 || cellX >= grid.Count)
                return false;
            List<byte> column = grid[cellX];
            byte value = cellY >= 0 && cellY < column.Count ? column[cellY] : (byte)0;
            if (!IsWalkableCell(cellX, cellY, grid.Count, column.Count, value))
                return false;

            pos = new PosInfo { X = x, Y = y };
            return true;
        }

        public static string RoomName(ERoomType room)
        {
            string key = room.ToString();
            var textDic = Managers.Data?.TextDic;
            if (textDic != null && textDic.TryGetValue(key, out var text) && text != null && !string.IsNullOrEmpty(text.Text))
                return text.Text;

            switch (room)
            {
                case ERoomType.CenterHall: return "中央广场";
                case ERoomType.Classroom: return "教室";
                default: return key;
            }
        }

        public static bool TryParseRoomName(string text, out ERoomType room)
        {
            room = default;
            string s = (text ?? string.Empty).Trim();
            if (s.Length == 0)
                return false;

            if (System.Enum.TryParse(s, ignoreCase: true, out ERoomType byEnum) && System.Enum.IsDefined(typeof(ERoomType), byEnum) && (byte)byEnum != 0)
            {
                room = byEnum;
                return true;
            }

            string lower = s.ToLowerInvariant();
            foreach (ERoomType candidate in System.Enum.GetValues(typeof(ERoomType)))
            {
                if ((byte)candidate == 0)
                    continue;
                string zh = RoomName(candidate);
                if (string.Equals(zh, s, System.StringComparison.OrdinalIgnoreCase))
                {
                    room = candidate;
                    return true;
                }
                if (candidate == ERoomType.CenterHall && (lower == "广场" || lower == "中央广场"))
                {
                    room = candidate;
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<ERoomType> RoomsOnMap()
        {
            var result = new List<ERoomType>();
            var grid = Managers.Data?.MapArray;
            if (grid == null || grid.Count == 0)
                return result;

            var seen = new HashSet<byte>();
            for (int x = 0; x < grid.Count; x++)
            {
                List<byte> column = grid[x];
                if (column == null) continue;
                for (int y = 0; y < column.Count; y++)
                {
                    byte b = column[y];
                    if (b == 0 || !seen.Add(b))
                        continue;
                    if (!System.Enum.IsDefined(typeof(ERoomType), (int)b))
                        continue;
                    result.Add((ERoomType)b);
                }
            }
            result.Sort((a, b) => ((byte)a).CompareTo((byte)b));
            return result;
        }

        public static string FormatRoomChoices()
        {
            var onMap = RoomsOnMap();
            IEnumerable<ERoomType> source = onMap.Count > 0
                ? (IEnumerable<ERoomType>)onMap
                : System.Enum.GetValues(typeof(ERoomType)).Cast<ERoomType>().Where(r => (byte)r != 0);
            return string.Join("、", source.Select(RoomName));
        }
    }
}
