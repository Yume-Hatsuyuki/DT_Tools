using System;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Features.BlackAttack
{
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.BlackKillLimit), MethodType.Getter)]
    internal static class BlackAttackKillLimitPatch
    {
        private static bool Prefix(ref int __result)
        {
            if (!Engine.Enabled<BlackAttackFeature>())
                return true;

            __result = Math.Max(0, BlackAttackFeature.KillLimit);
            return false;
        }
    }
}
