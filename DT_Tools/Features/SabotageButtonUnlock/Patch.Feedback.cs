using System.Collections;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    [HarmonyPatch(typeof(Door), "UseSabotage")]
    internal static class DoorLockAttemptPatch
    {
        private static void Prefix()
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return;
            if (Managers.Player.MyPlayer.Color == EPlayerColor.Dark)
                return;

            SabotageButtonUnlockLogic.RecordLockAttempt();
        }
    }

    [HarmonyPatch]
    internal static class SabotageCooltimeAckPatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_COOLTIME_SABOTAGE");

        private static void Postfix()
        {
            SabotageButtonUnlockLogic.RecordLockAck();
        }
    }

    [HarmonyPatch]
    internal static class DestroyEvidenceAckPatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_COOLTIME_DESTROY_EVIDENCE");

        private static void Postfix()
        {
            SabotageButtonUnlockLogic.RecordDestroyAck();
        }
    }
}
