using System;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Patches.System.BlackAttack
{
    /// <summary>
    /// BlackKillLimit 改写：原版开局人数 &lt; BLACK_DOUBLE_KILL_MIN_PLAYER(6) 时 1，否则 2。
    /// 属性 getter，0.1.15b GameRoom.cs:131。
    /// </summary>
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
