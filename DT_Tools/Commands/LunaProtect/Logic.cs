using System.Collections.Generic;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.LunaProtect
{
    /// <summary>
    /// /luna_protect 业务：阶段守卫 + S_NOTIFY_LUNA_ABILITY 广播。
    ///
    /// Luna 护盾不是 BUFF，而是客户端 MyPlayer.HasLunaShield 判定：亮灯时，Luna 本人或
    /// 客户端 LunaAbilityIds 名单内的玩家，Black 出刀会被拦截（播放 RunaShieldSfx，
    /// 不发送 C_KILL_PLAYER）。名单的原版同步方式是 Soi 用 RuleBreaker 偷到 Luna 技能后
    /// 服务端广播 S_NOTIFY_LUNA_ABILITY，本命令发送同一个包；该包每包只携带一个
    /// PlayerId，all 时对每名玩家各广播一包（room.Broadcast 走全体客户端含房主自身）。
    /// </summary>
    internal static class LunaProtectLogic
    {
        /// <summary>房间守卫：切换中 / 迁移中 / 非生存阶段拒绝。</summary>
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
                text = "正在进行主机迁移，无法施加护盾。";
                return false;
            }
            if (room.State != EGameState.Survive)
            {
                code = "invalid state";
                text = $"护盾命令仅能在生存阶段（Survive）使用，当前状态: {room.State}。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }

        /// <summary>对名单内玩家逐个广播护盾包（每个 PlayerId 一包）。</summary>
        public static void Apply(GameRoom room, IEnumerable<Server.Game.Player> targets)
        {
            foreach (var player in targets)
            {
                room.Broadcast(new S_NOTIFY_LUNA_ABILITY   // 0.1.15b Protocol/S_NOTIFY_LUNA_ABILITY.cs:8
                {
                    PlayerId = player.PublicInfo.PlayerId,
                });
            }
        }

        /// <summary>单目标护盾校验：观战者 / 已死亡无意义。</summary>
        public static bool IsTargetProtectable(Server.Game.Player target, int targetId, out string code, out string text)
        {
            if (target.IsSpectator)
            {
                code = "target is spectator";
                text = $"目标 {target.Name}（#{targetId}）是观战者，护盾无意义。";
                return false;
            }
            if (!target.IsAlive)
            {
                code = "target already dead";
                text = $"目标 {target.Name}（#{targetId}）已死亡，无法套用护盾。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }
    }
}
