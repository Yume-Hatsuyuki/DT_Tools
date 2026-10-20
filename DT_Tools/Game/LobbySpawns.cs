using System;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Game
{
    /// <summary>
    /// 大厅出生点覆盖（房主侧，仅当前房间有效）。
    /// </summary>
    /// <remarks>
    /// 键为玩家 AccountID。覆盖点取目标房间格子中，最接近房间质心的那一格的中心。
    /// 格子换算与服务端 AreaManager 一致：格子 = floor(坐标 / 224) - MapOffset（0.1.18c Server.Game/AreaManager.cs:74-80、82-96）。
    /// 格子房间类型来自 DataManager.MapArray（0.1.18c DataManager.cs:28）。
    /// 原版大厅点 MapData.LobbyPos（Data/MapData.cs:16）在两处使用：新玩家构造（Server.Game/Player.cs:555）
    /// 与每轮结束回大厅（Server.Game/GameRoom.cs:868）。
    /// </remarks>
    public static class LobbySpawns
    {
        /// <summary>地图格子边长（与 AreaManager.ValidPosition 的 224 相同）。</summary>
        public const float GridScale = 224f;

        private static readonly Dictionary<string, ERoomType> _targets = new Dictionary<string, ERoomType>();

        /// <summary>设置玩家的大厅目标房间。</summary>
        public static void Set(string accountId, ERoomType room) => _targets[accountId] = room;

        /// <summary>移除玩家的覆盖，恢复为地图大厅点。</summary>
        public static bool Remove(string accountId) => _targets.Remove(accountId);

        /// <summary>查询玩家的覆盖房间。</summary>
        public static bool TryGet(string accountId, out ERoomType room)
        {
            if (!string.IsNullOrEmpty(accountId))
                return _targets.TryGetValue(accountId, out room);
            room = default;
            return false;
        }

        /// <summary>清空所有覆盖。</summary>
        public static void Clear() => _targets.Clear();

        /// <summary>当前所有覆盖的只读快照。</summary>
        public static IReadOnlyDictionary<string, ERoomType> Snapshot() => new Dictionary<string, ERoomType>(_targets);

        /// <summary>
        /// 纯函数：格子 (cellX, cellY) 的中心坐标。
        /// cellX、cellY 为 MapArray 的下标；offsetX、offsetY 为 MapData.MapOffset。
        /// </summary>
        public static (float X, float Y) CellCenter(int cellX, int cellY, int offsetX, int offsetY) =>
            ((cellX + offsetX + 0.5f) * GridScale, (cellY + offsetY + 0.5f) * GridScale);

        /// <summary>
        /// 纯函数：从房间的全部格子中选出最接近质心的一格。cells 为空时抛出异常。
        /// </summary>
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

        /// <summary>
        /// 在当前地图中为房间选出出生点中心。该房间不存在于地图中时返回 false。
        /// </summary>
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

        /// <summary>
        /// 纯函数：坐标所在的格子下标。与服务端 AreaManager.GetArea / ValidPosition 相同（0.1.18c Server.Game/AreaManager.cs:76-78、85-86）。
        /// </summary>
        public static int CellIndex(float coordinate, int mapOffset) => (int)(coordinate / GridScale) - mapOffset;

        /// <summary>
        /// 纯函数：格子下标是否在地图内且房间类型非 0（0.1.18c AreaManager.cs:87-94 的判定）。
        /// </summary>
        public static bool IsWalkableCell(int cellX, int cellY, int width, int height, byte value) =>
            cellX >= 0 && cellY >= 0 && cellX < width && cellY < height && value != 0;

        /// <summary>
        /// 自定义坐标：在地图内且所在格子有效时返回该坐标（原样使用，不吸附到格子中心）。
        /// 无效时返回 false，调用方应回退为地图大厅点。
        /// </summary>
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

        /// <summary>
        /// 房间显示名：优先 TextDic 本地化（与 RoomLabel / 自动化出生点同源），缺省回退枚举名。
        /// 内置少量常用中文别名，避免 TextDic 未加载时只显示英文。
        /// </summary>
        public static string RoomName(ERoomType room)
        {
            string key = room.ToString();
            var textDic = Managers.Data?.TextDic;
            if (textDic != null && textDic.TryGetValue(key, out var text) && text != null && !string.IsNullOrEmpty(text.Text))
                return text.Text;

            // TextDic 未就绪时的兜底中文（与游戏常见译名一致）
            switch (room)
            {
                case ERoomType.CenterHall: return "中央广场";
                case ERoomType.Classroom: return "教室";
                default: return key;
            }
        }

        /// <summary>
        /// 解析房间名：接受枚举名（CenterHall）、中文显示名（TextDic / 兜底）、以及少量别名。
        /// </summary>
        public static bool TryParseRoomName(string text, out ERoomType room)
        {
            room = default;
            string s = (text ?? string.Empty).Trim();
            if (s.Length == 0)
                return false;

            // 1) 直接按枚举名（忽略大小写）
            if (System.Enum.TryParse(s, ignoreCase: true, out ERoomType byEnum) && System.Enum.IsDefined(typeof(ERoomType), byEnum) && (byte)byEnum != 0)
            {
                room = byEnum;
                return true;
            }

            // 2) 与 TextDic 本地化名 / RoomName 兜底名精确匹配（忽略大小写）
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
                // 别名：中央广场 / 广场
                if (candidate == ERoomType.CenterHall && (lower == "广场" || lower == "中央广场"))
                {
                    room = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 当前地图 MapArray 上实际出现的房间类型（去重、按枚举值排序，排除 0=不可通行）。
        /// 地图未加载时返回空列表。
        /// </summary>
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

        /// <summary>
        /// 供命令 help / list 使用：当前地图房间的「中文名」列表；地图未就绪时列出全部枚举中文名。
        /// </summary>
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
