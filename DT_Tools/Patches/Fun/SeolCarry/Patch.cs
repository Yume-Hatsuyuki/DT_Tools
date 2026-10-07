using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.SeolCarry
{
    /// <summary>Seol 搬人模式状态与传送执行（服务端进程内共享状态）。</summary>
    internal static class SeolCarryLogic
    {
        /// <summary>搬人模式：Seol 的 PlayerId → 时停结束位置 P（存在即处于搬人模式）。</summary>
        private static readonly Dictionary<int, PosInfo> _carryMode = new Dictionary<int, PosInfo>();

        /// <summary>时停结束：记录位置并进入搬人模式（技能键恢复可用）。</summary>
        public static void EnterCarryMode(Server.Game.Player seol)
        {
            if (seol == null || !seol.IsAlive) return;
            _carryMode[seol.PublicInfo.PlayerId] = seol.PublicInfo.Pos.Clone();
            seol.CanUseSkill = true; // 绕过冷却，让客户端技能键重新亮起
            // 私发提示（仅 Seol 可见）
            seol.Session?.Send(new S_CHAT_MESSAGE
            {
                Type = EChatType.NormalChat,
                Text = "[Seol搬人] 时停结束，已记录你的位置：对准某玩家按技能键，将其传送到这里",
                PlayerId = 0
            });
            Log.Info<SeolCarryFeature>($"Seol 进入搬人模式：{seol.Name} @ {seol.PublicInfo.Pos.X},{seol.PublicInfo.Pos.Y}");
        }

        /// <summary>是否处于搬人模式（Seol 且有时停结束位置记录）。</summary>
        public static bool IsCarryMode(Server.Game.Player seol)
            => seol != null && _carryMode.ContainsKey(seol.PublicInfo.PlayerId);

        /// <summary>
        /// 执行搬人：给目标玩家加传送流程（Stop + Casting + 延迟后 Move 到 P）。
        /// 返回 true 表示已拦截原技能并完成搬人。
        /// </summary>
        public static bool TryCarry(Server.Game.Player seol, C_USE_SKILL pkt)
        {
            if (seol == null || pkt == null) return false;
            if (!_carryMode.TryGetValue(seol.PublicInfo.PlayerId, out var dest)) return false;

            // 先清搬人模式并恢复冷却（无论目标是否有效，本次按键即消费）
            _carryMode.Remove(seol.PublicInfo.PlayerId);
            seol.CanUseSkill = false;
            // 恢复原时停冷却（对齐原版 60 秒）
            var coolJob = Server.Game.TimeManager.Instance.PushSurvivalJob(SeolCarryFeature.CooldownSec, delegate
            {
                if (seol.IsAlive && seol.SkillComponent?.Data?.Type == ESkillType.TimeStop)
                    seol.CanUseSkill = true;
            });
            _ = coolJob;

            var target = Server.Game.GameRoom.Instance.AlivePlayers
                .FirstOrDefault(x => x.PublicInfo.PlayerId == pkt.TargetId);
            if (target == null || target == seol)
            {
                seol.Session?.Send(new S_CHAT_MESSAGE
                {
                    Type = EChatType.NormalChat,
                    Text = "[Seol搬人] 未对准有效目标，本次未传送",
                    PlayerId = 0
                });
                return true; // 仍拦截原技能（避免误触发时停）
            }

            // 复制原版传送流程（0.1.16b UseTeleport）：Stop + Casting + 延迟后强制 Move
            target.BuffComponent.AddBuff(EBuffType.Stop, 3600000, isBroadcast: false);
            target.State = EPlayerState.Casting;
            Server.Game.GameRoom.Instance.PushAfter(SeolCarryFeature.CarryDelayMs, delegate
            {
                if (target.IsAlive && Server.Game.GameRoom.Instance.State == EGameState.Survive)
                {
                    target.BuffComponent.RemoveBuffForce(EBuffType.Stop);
                    target.State = EPlayerState.Idle;
                    target.Move(dest, force: true);
                    Server.Game.GameRoom.Instance.BroadcastWorldVFX(EEffectType.TeleportVfx, target.PublicInfo.PlayerId, target.PublicInfo.Pos);
                }
            });
            Log.Info<SeolCarryFeature>($"Seol 搬人：{target.Name} 传送到 {dest.X},{dest.Y}");
            return true;
        }
    }

    /// <summary>
    /// 时停使用（0.1.16b SkillComponent.UseTimeStop，private）：Postfix 挂 5 秒时停结束
    /// 检测——TheWorld 持续 5000ms，结束时记录 Seol 位置并进入搬人模式。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.SkillComponent), "UseTimeStop")]
    internal static class SeolCarryTimeStopPatch
    {
        private static void Postfix(Server.Game.SkillComponent __instance)
        {
            try
            {
                if (!Engine.Enabled<SeolCarryFeature>())
                    return;
                var owner = __instance?.Owner;
                if (owner == null) return;

                // TheWorld 固定 5000ms：结束后记录位置（时停期间 Seol 正常行动）
                Server.Game.GameRoom.Instance.PushAfter(5000, delegate
                {
                    SeolCarryLogic.EnterCarryMode(owner);
                });
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<SeolCarryFeature>("Seol 时停搬人登记失败（可忽略）：" + ex.Message);
            }
        }
    }

    /// <summary>
    /// 技能使用入口（0.1.16b SkillComponent.UseSkill(C_USE_SKILL)，public）：Seol 处于
    /// 搬人模式时拦截原技能（不触发时停），改为执行搬人。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.SkillComponent), "UseSkill")]
    internal static class SeolCarryUsePatch
    {
        private static bool Prefix(Server.Game.SkillComponent __instance, C_USE_SKILL pkt)
        {
            try
            {
                if (!Engine.Enabled<SeolCarryFeature>())
                    return true; // 未开启：放行原逻辑

                var owner = __instance?.Owner;
                if (owner == null) return true;
                if (!SeolCarryLogic.IsCarryMode(owner))
                    return true;

                return !SeolCarryLogic.TryCarry(owner, pkt); // 搬人成功则拦截原技能
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<SeolCarryFeature>("Seol 搬人执行失败（放行原逻辑）：" + ex.Message);
                return true;
            }
        }
    }
}
