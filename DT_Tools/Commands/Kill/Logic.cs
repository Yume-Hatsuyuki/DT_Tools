using System.Collections.Generic;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Kill
{
    internal static class KillLogic
    {
        private const int PrimeBuffMs = 6000;

        public static bool TryGuard(GameRoom room, out string code, out string text)
        {
            if (!RoomFlow.TryGuardBusy(room, "执行处决", out code, out text))
                return false;
            if (!IsAllowedState(room.State))
            {
                code = "invalid state";
                text = $"处决仅能在生存/调查/裁判阶段使用，当前状态: {room.State}。";
                return false;
            }
            return true;
        }

        public static bool IsAllowedState(EGameState state)
            => state == EGameState.Survive
               || state == EGameState.Detective
               || state == EGameState.Trial;

        public static bool IsTargetKillable(Server.Game.Player target, out string text, out string code)
        {
            int id = target.PublicInfo.PlayerId;
            if (target.IsSpectator)
            {
                code = "target is spectator";
                text = $"目标 {target.Name}（#{id}）是观战者，无法处决。";
                return false;
            }
            if (!target.IsAlive)
            {
                code = "target already dead";
                text = $"目标 {target.Name}（#{id}）已死亡，无需处决。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }

        public static void Execute(GameRoom room, List<Server.Game.Player> targets, bool isTrial)
        {
            if (!isTrial)
            {
                foreach (var t in targets)
                {
                    t.BuffComponent.AddBuff(EBuffType.DeadDetective, PrimeBuffMs, isBroadcast: false);
                    room.Broadcast(new S_PLAY_EFFECT
                    {
                        Type = EEffectType.ScopeVfx,
                        DeviceId = t.PublicInfo.PlayerId,
                        Pos = t.PublicInfo.Pos,
                    });
                }
                room.BroadcastSystemSFX(ESoundType.LouisSkillSfx);
            }

            foreach (var t in targets)
                t.OnDeadCollarBomb();
        }
    }
}
