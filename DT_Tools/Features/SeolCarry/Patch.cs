using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SeolCarry
{
    internal static class SeolCarryLogic
    {
        private static readonly Dictionary<int, PosInfo> _carryMode = new Dictionary<int, PosInfo>();

        public static void EnterCarryMode(Server.Game.Player seol)
        {
            if (seol == null || !seol.IsAlive) return;
            _carryMode[seol.PublicInfo.PlayerId] = seol.PublicInfo.Pos.Clone();
            seol.CanUseSkill = true;
            seol.Session?.Send(new S_CHAT_MESSAGE
            {
                Type = EChatType.NormalChat,
                Text = "[Seol搬人] 时停结束，已记录你的位置：对准某玩家按技能键，将其传送到这里",
                PlayerId = 0
            });
            Log.Info<SeolCarryFeature>($"Seol 进入搬人模式：{seol.Name} @ {seol.PublicInfo.Pos.X},{seol.PublicInfo.Pos.Y}");
        }

        public static bool IsCarryMode(Server.Game.Player seol)
            => seol != null && _carryMode.ContainsKey(seol.PublicInfo.PlayerId);

        public static bool TryCarry(Server.Game.Player seol, C_USE_SKILL pkt)
        {
            if (seol == null || pkt == null) return false;
            if (!_carryMode.TryGetValue(seol.PublicInfo.PlayerId, out var dest)) return false;

            _carryMode.Remove(seol.PublicInfo.PlayerId);
            seol.CanUseSkill = false;
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
                return true;
            }

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

                Server.Game.GameRoom.Instance.PushAfter(5000, delegate
                {
                    SeolCarryLogic.EnterCarryMode(owner);
                });
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<SeolCarryFeature>(ex, "Seol 时停搬人登记失败（可忽略）");
            }
        }
    }

    [HarmonyPatch(typeof(Server.Game.SkillComponent), "UseSkill")]
    internal static class SeolCarryUsePatch
    {
        private static bool Prefix(Server.Game.SkillComponent __instance, C_USE_SKILL pkt)
        {
            try
            {
                if (!Engine.Enabled<SeolCarryFeature>())
                    return true;

                var owner = __instance?.Owner;
                if (owner == null) return true;
                if (!SeolCarryLogic.IsCarryMode(owner))
                    return true;

                return !SeolCarryLogic.TryCarry(owner, pkt);
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<SeolCarryFeature>(ex, "Seol 搬人执行失败（放行原逻辑）");
                return true;
            }
        }
    }
}
