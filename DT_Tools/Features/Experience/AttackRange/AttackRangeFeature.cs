using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 黑方攻击距离。GetTargetPlayer 中原版硬编码 224f → Range。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "GetTargetPlayer")]
    [PatchFeature(
        section: "GetTargetPlayer",
        description: "黑方攻击距离：可修改最大攻击距离（默认 224）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class AttackRangeFeature
    {
        [ConfigField(224f, "黑方攻击可命中的最大距离，游戏默认为 224。")]
        public static ConfigEntry<float> Range;

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance, ref Player __result)
        {
            if (__instance.Inventory.Weapon.DataId == 0)
            {
                __result = null;
                return false;
            }

            Player best = null;
            float bestDist = float.PositiveInfinity;
            float range = Range.Value;

            foreach (Player player in Managers.Player.Players.Values)
            {
                if (player.State == EPlayerState.Hide)
                    continue;

                Vector2 direction = player.Position - __instance.Position;
                float magnitude = direction.magnitude;
                if (magnitude > range)
                    continue;

                if (!Physics2D.Raycast(__instance.Position, direction, magnitude, 12288) &&
                    magnitude < bestDist)
                {
                    best = player;
                    bestDist = magnitude;
                }
            }

            __result = best;
            return false;
        }
    }
}
