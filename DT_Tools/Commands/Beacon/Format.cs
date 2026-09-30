using System.Collections.Generic;
using System.Linq;
using System.Text;
using DT_Tools.Game;
using Data;
using Protocol;

namespace DT_Tools.Commands.Beacon
{
    /// <summary>/beacon 输出格式化：人类回复文本 + JSON 结果 DTO（匿名对象，键名沿用旧 JSON）。</summary>
    internal static class BeaconFormat
    {
        public static string HelpAndSpawns(MapData mapData, MyPlayer my)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 战略信标 · 掉皮掉肉不掉队 ━━━");
            sb.AppendLine("瞬移到指定坐标。直接发包 C_MOVE，服务端无距离校验。");
            sb.AppendLine();

            // 当前坐标
            sb.AppendLine("【当前位置】");
            var curRoom = RoomLabel.FromPos(my.PublicInfo.Pos);
            sb.AppendLine($"  ({my.PublicInfo.Pos.X:F0}, {my.PublicInfo.Pos.Y:F0})  状态={my.State}  区域=[{curRoom.localized}]");
            sb.AppendLine();

            // 在线玩家（#id 即 /beacon player 的目标参数）
            sb.AppendLine("【在线玩家 Players】（用 /beacon player #id 瞬移到其身边）");
            int myId = my.PublicInfo.PlayerId;
            AppendPlayerLine(sb, myId, my.Name, my.PublicInfo.Pos, my.State, self: true);
            foreach (var p in Managers.Player.Players.Values.OrderBy(p => p.PublicInfo.PlayerId))
            {
                AppendPlayerLine(sb, p.PublicInfo.PlayerId, p.Name, p.TargetPos ?? p.PublicInfo.Pos,
                    p.State, p.PublicInfo.PlayerId == myId);
            }
            sb.AppendLine();

            // 出生点
            sb.AppendLine("【默认出生点 StartPosList】（用 /beacon spawn #i 瞬移）");
            var list = mapData.StartPosList;
            if (list == null || list.Count == 0)
            {
                sb.AppendLine("  （无）— 不在对局地图 / 未加载（大厅阶段为空属正常）");
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var r = RoomLabel.FromPos(list[i]);
                    sb.AppendLine($"  #{i + 1,-2} ({list[i].X:F0}, {list[i].Y:F0})  →  {r.localized}");
                }
            }
            sb.AppendLine();

            // 大厅 / 兜底点
            sb.AppendLine("【其它坐标】");
            sb.AppendLine($"  lobby  (大厅点)  ({FormatPos(mapData.LobbyPos)})  →  {RoomLabel.FromPos(mapData.LobbyPos).localized}");
            sb.AppendLine($"  error  (兜底点)  ({FormatPos(mapData.ErrorPos)})  →  {RoomLabel.FromPos(mapData.ErrorPos).localized}");
            sb.AppendLine();

            // 区域说明
            sb.AppendLine("【区域出生点】");
            sb.AppendLine("  区域（RoomData）无固定出生坐标：仅含 DataId/Type/AdjacentArea/PrefabName，");
            sb.AppendLine("  不含 Pos 字段；出生点统一存于 MapData.StartPosList（地图级，非区域级）。");
            sb.AppendLine("  房间名按 224 网格反查 MapArray → ERoomType → TextDic 本地化名得到。");
            sb.AppendLine();

            // 用法
            sb.AppendLine("【用法】");
            sb.AppendLine("  /beacon              列出出生点 / 在线玩家坐标 + 帮助");
            sb.AppendLine("  /beacon <x> <y>      瞬移到指定坐标");
            sb.AppendLine("  /beacon spawn #i     瞬移到第 i 个出生点（1 起，# 可省略）");
            sb.AppendLine("  /beacon player #id   瞬移到 #id 号玩家当前位置（# 可省略）");
            sb.AppendLine("  /beacon lobby        瞬移到大厅点");
            sb.AppendLine("  /beacon error        瞬移到兜底点 ErrorPos");
            sb.AppendLine();
            sb.AppendLine("【注意】");
            sb.AppendLine("  - Hide/Sit 状态服务端拒收 C_MOVE，请先脱离藏身处/座位");
            sb.AppendLine("  - 坐标超出可行走网格：生存/调查阶段服务端强制改到 ErrorPos，其余阶段移动被静默丢弃");
            sb.AppendLine("  - player 目标须在存活玩家表内：死亡（S_DESPAWN）/旁观者无法定位");
            sb.AppendLine("  - 跟随控制台 / 客户端：非房主可用，仅作用于本机");
            return sb.ToString();
        }

        /// <summary>无参列表的 JSON（键名沿用旧实现）：spawns=数量，list=出生点，players=在线玩家。</summary>
        public static object ListResult(MapData mapData, MyPlayer my)
        {
            var spawns = new List<object>();
            var list = mapData.StartPosList;
            if (list != null)
            {
                foreach (var p in list)
                {
                    var room = RoomLabel.FromPos(p);
                    spawns.Add(new { x = p.X, y = p.Y, room = room.raw });
                }
            }

            int myId = my.PublicInfo.PlayerId;
            var players = new List<object>
            {
                PlayerDto(myId, my.Name, my.PublicInfo.Pos, my.State, self: true),
            };
            foreach (var p in Managers.Player.Players.Values.OrderBy(p => p.PublicInfo.PlayerId))
            {
                players.Add(PlayerDto(p.PublicInfo.PlayerId, p.Name, p.TargetPos ?? p.PublicInfo.Pos,
                    p.State, p.PublicInfo.PlayerId == myId));
            }

            return new { spawns = spawns.Count, list = spawns, players };
        }

        /// <summary>瞬移成功的 JSON（键名沿用旧实现；targetId 仅 player 目标时回带）。</summary>
        public static object TeleportResult(Protocol.PosInfo pos, string roomRaw, int targetPlayerId)
        {
            if (targetPlayerId > 0)
                return new { x = pos.X, y = pos.Y, room = roomRaw, targetId = targetPlayerId };
            return new { x = pos.X, y = pos.Y, room = roomRaw };
        }

        private static void AppendPlayerLine(StringBuilder sb, int id, string name, Protocol.PosInfo pos,
            EPlayerState state, bool self)
        {
            string tag = self ? "(我)" : "   ";
            string room = RoomLabel.FromPos(pos).localized;
            sb.AppendLine($"  {tag} #{id,-2} {name}  ({pos.X:F0}, {pos.Y:F0})  状态={state}  区域=[{room}]");
        }

        private static object PlayerDto(int id, string name, Protocol.PosInfo pos, EPlayerState state, bool self)
        {
            return new
            {
                id,
                name = name ?? "",
                x = pos.X,
                y = pos.Y,
                state = state.ToString(),
                room = RoomLabel.FromPos(pos).raw,
                me = self,
            };
        }

        private static string FormatPos(Protocol.PosInfo p)
            => p == null ? "未配置" : $"{p.X:F0}, {p.Y:F0}";
    }
}
