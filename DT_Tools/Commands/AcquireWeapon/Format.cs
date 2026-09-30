using System.Collections.Generic;
using System.Linq;
using System.Text;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.AcquireWeapon
{
    /// <summary>/acquire_weapon 输出格式化：人类回复文本 + JSON 结果 DTO（键名沿用旧 JSON）。</summary>
    internal static class AcquireWeaponFormat
    {
        public static string ListReply(List<DeviceBase> armories, List<DeviceBase> open)
        {
            var sb = new StringBuilder();
            if (open.Count == 0)
            {
                sb.AppendLine("当前没有开放中的武器架（凶器未刷新 / 已被取走）。");
            }
            else
            {
                sb.AppendLine($"━━━ 可获取武器（共 {open.Count} 个）━━━");
                for (int i = 0; i < open.Count; i++)
                {
                    var cur = open[i];
                    var (localized, raw) = RoomLabel.FromDevice(cur);
                    var pos = cur.Info?.Pos;
                    sb.AppendLine($"  武器架 ID：#{cur.ID}");
                    sb.AppendLine($"  判定：{AcquireWeaponLogic.JudgeArmory(cur)}");
                    sb.AppendLine($"  所在地区：{localized}（{raw}）");
                    sb.AppendLine($"  坐标：({pos?.X ?? 0}, {pos?.Y ?? 0})");
                    int? remain = AcquireWeaponLogic.TryGetMoveRemainSeconds(cur);
                    if (remain.HasValue)
                        sb.AppendLine($"  换点倒计时：约 {remain.Value} 秒后无人取走则自动移动");
                    if (i < open.Count - 1)
                        sb.AppendLine("----------------------------------");
                }
                sb.Append("  → /天匠 #<ID> 拔刀");
            }

            sb.AppendLine();
            sb.AppendLine($"━━━ 全部武器架（共 {armories.Count} 个）━━━");
            for (int i = 0; i < armories.Count; i++)
            {
                var a = armories[i];
                var (loc, rawRoom) = RoomLabel.FromDevice(a);
                string stateText = StateLabel(a.DeviceState);
                string line = $"  #{a.ID,-4} {stateText}  {loc}（{rawRoom}）";
                if (a.DeviceState == (int)EArmoryState.OpenArmory)
                {
                    int? remain = AcquireWeaponLogic.TryGetMoveRemainSeconds(a);
                    if (remain.HasValue) line += $"  剩余 {remain.Value}s";
                    if (AcquireWeaponLogic.IsServerCurrentArmory(a))
                        line += "  ◄ 当前凶器";
                }
                sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }

        public static object ListResult(List<DeviceBase> armories, List<DeviceBase> open)
        {
            return new
            {
                mode = "list",
                open = open.Select(a => OpenDto(a)),
                armories = armories.SelectDto(),
            };
        }

        /// <summary>开放架 JSON：附真伪判定（pickable = real/conditional/unknown）。</summary>
        private static object OpenDto(DeviceBase a)
        {
            var (loc, raw) = RoomLabel.FromDevice(a);
            var pos = a.Info?.Pos;
            int? remain = AcquireWeaponLogic.TryGetMoveRemainSeconds(a);
            return new
            {
                id = a.ID,
                state = ((EArmoryState)a.DeviceState).ToString(),
                room = raw,
                roomName = loc,
                x = pos?.X ?? 0,
                y = pos?.Y ?? 0,
                remainSeconds = remain,
                pickable = AcquireWeaponLogic.PickableKind(a),
            };
        }

        public static string AcquireReply(int armoryId, EArmoryInteractType interactType, string role)
        {
            var msg = new StringBuilder();
            msg.Append($"【天匠】已发送 C_INTERACT_ARMORY：刀架 #{armoryId}，类型 {interactType}（{role}），无视距离。");
            msg.Append($"\n服务端无成功回执：自己背包出现凶器 {AcquireWeaponLogic.WeaponDataId} / 颜色变为 Black 即生效；");
            msg.Append("黑幕截刀后会启动凶器回收倒计时。");
            msg.Append("\n注意：非开放刀架会被服务端静默拒绝；开放刀架的可取性见 /天匠 列表判定（房主启用「武器架多刀」时全部开放架可拔）。");
            return msg.ToString();
        }

        private static string StateLabel(int state)
        {
            switch ((EArmoryState)state)
            {
                case EArmoryState.LockedArmory: return "锁定";
                case EArmoryState.OpenArmory:   return "开放";
                case EArmoryState.EmptyArmory:  return "空置";
                default:                        return "未知(" + state + ")";
            }
        }

        /// <summary>单个刀架 JSON（键名沿用旧实现：id/state/room/roomName/x/y/remainSeconds）。</summary>
        private static object ToDto(this DeviceBase a)
        {
            var (loc, raw) = RoomLabel.FromDevice(a);
            var pos = a.Info?.Pos;
            if (a.DeviceState == (int)EArmoryState.OpenArmory)
            {
                int? remain = AcquireWeaponLogic.TryGetMoveRemainSeconds(a);
                if (remain.HasValue)
                {
                    return new
                    {
                        id = a.ID,
                        state = ((EArmoryState)a.DeviceState).ToString(),
                        room = raw,
                        roomName = loc,
                        x = pos?.X ?? 0,
                        y = pos?.Y ?? 0,
                        remainSeconds = remain.Value,
                    };
                }
            }
            return new
            {
                id = a.ID,
                state = ((EArmoryState)a.DeviceState).ToString(),
                room = raw,
                roomName = loc,
                x = pos?.X ?? 0,
                y = pos?.Y ?? 0,
            };
        }

        private static List<object> SelectDto(this List<DeviceBase> armories)
        {
            var list = new List<object>(armories.Count);
            foreach (var a in armories)
                list.Add(a.ToDto());
            return list;
        }
    }
}
