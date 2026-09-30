using System;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GiveBuff
{
    /// <summary>/givebuff 业务：目标筛选、BUFF 添加/清除执行。</summary>
    internal static class GiveBuffLogic
    {
        /// <summary>
        /// 对 all 或指定玩家执行 action。失败返回 false（错误码与提示已给出）；成功返回处理人数。
        /// requireAlive=true 时跳过/拒绝死亡玩家（添加 BUFF 需存活，清除不需要）。
        /// </summary>
        public static bool ApplyToTargets(
            GameRoom room,
            GiveBuffArgs args,
            Action<Server.Game.Player> action,
            bool requireAlive,
            out int count,
            out string code,
            out string text)
        {
            if (args.All)
            {
                count = 0;
                foreach (var player in room.Players)
                {
                    if (player?.PublicInfo == null) continue;
                    if (requireAlive && !player.IsAlive) continue;
                    action(player);
                    count++;
                }
                code = null;
                text = null;
                return true;
            }

            var found = PlayerQuery.FindById(room, args.PlayerId);
            if (found == null)
            {
                count = 0;
                code = "target not found";
                text = $"找不到 PlayerId={args.PlayerId} 的玩家。";
                return false;
            }
            if (requireAlive && !found.IsAlive)
            {
                count = 0;
                code = "target already dead";
                text = $"玩家 {found.Name}（#{args.PlayerId}）已死亡，无法添加 BUFF。";
                return false;
            }
            action(found);
            count = 1;
            code = null;
            text = null;
            return true;
        }

        /// <summary>
        /// 添加 BUFF 并兜底到期。原版 AddBuff 在已有同类时直接忽略，这里先强制移除再加
        /// 以刷新时长；原版 Flush 只遍历 AlivePlayers，大厅列表为空导致不会自动到期，
        /// 故额外 PushAfter(durationMs) 调 RemoveBuffForce 兜底
        /// （0.1.15b Server.Game/BuffComponent.cs:93 AddBuff / :118 RemoveBuffForce，
        ///  Server.Game/JobSerializer.cs:98 PushAfter）。
        /// </summary>
        public static void AddBuffWithRefresh(GameRoom room, Server.Game.Player player, EBuffType buffType, int durationMs)
        {
            if (player.BuffComponent.HasBuff(buffType))
                player.BuffComponent.RemoveBuffForce(buffType);

            player.BuffComponent.AddBuff(buffType, durationMs);

            int pid = player.PublicInfo.PlayerId;
            room.PushAfter(durationMs, () =>
            {
                var p = room.Players.Find(x => x?.PublicInfo?.PlayerId == pid);
                if (p == null) return;
                if (p.BuffComponent.HasBuff(buffType))
                    p.BuffComponent.RemoveBuffForce(buffType);
            });
        }
    }
}
