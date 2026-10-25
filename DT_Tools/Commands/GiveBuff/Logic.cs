using System;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GiveBuff
{
    internal static class GiveBuffLogic
    {
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
