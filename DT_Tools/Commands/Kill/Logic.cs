using System.Collections.Generic;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Kill
{
    /// <summary>/kill 业务：房间门禁、目标校验、颈环炸弹处决执行。</summary>
    internal static class KillLogic
    {
        /// <summary>DeadDetective 语义标记时长（毫秒）；CollarBomb 分支不检查它，到期自然过期。</summary>
        private const int PrimeBuffMs = 6000;

        /// <summary>房间门禁：切换中 / 迁移中 / 阶段不符时返回 false，并给出错误码与提示文案。</summary>
        public static bool TryGuard(GameRoom room, out string code, out string text)
        {
            if (room.IsTransitioning)
            {
                code = "transitioning";
                text = "阶段切换正在进行中（等待全体客户端加载完成），请稍后再试。";
                return false;
            }
            if (room.IsMigrating)
            {
                code = "migrating";
                text = "正在进行主机迁移，无法执行处决。";
                return false;
            }
            if (!IsAllowedState(room.State))
            {
                code = "invalid state";
                text = $"处决仅能在生存/调查/裁判阶段使用，当前状态: {room.State}。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }

        public static bool IsAllowedState(EGameState state)
            => state == EGameState.Survive
               || state == EGameState.Detective
               || state == EGameState.Trial;

        /// <summary>单目标校验：观战者 / Dummy（主机占位）/ 已死亡不可处决。</summary>
        public static bool IsTargetKillable(Server.Game.Player target, out string text, out string code)
        {
            int id = target.PublicInfo.PlayerId;
            if (target.IsSpectator)
            {
                code = "target is spectator";
                text = $"目标 {target.Name}（#{id}）是观战者，无法处决。";
                return false;
            }
            if (target.IsDummy)
            {
                code = "target is dummy";
                text = $"目标 {target.Name}（#{id}）是 Dummy（主机占位），无法处决。";
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

        /// <summary>
        /// 执行处决：非审判阶段先预热（DeadDetective 标记 + Louis 瞄准镜 + 警告音），
        /// 再逐个调用原版 OnDeadCollarBomb——先立即广播 DyingVfx 倒地，PushAfter(6000)
        /// 后才真死亡（0.1.15b Server.Game/Player.cs:857-878，JobTimer 按原版触发）。
        /// </summary>
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
                // 全员警告音只播一次，避免叠加刺耳
                room.BroadcastSystemSFX(ESoundType.LouisSkillSfx);
            }

            foreach (var t in targets)
                t.OnDeadCollarBomb();
        }
    }
}
