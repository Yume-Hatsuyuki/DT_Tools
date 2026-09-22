using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 递刀距离。GetHandWeaponTarget 中原版硬编码 224f → Range；仍排除 Hide / KnownBlackIds。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "GetHandWeaponTarget")]
    [PatchFeature(
        section: "GetHandWeaponTarget",
        description: "递刀距离：可修改黑幕最大递交武器的距离（默认 224）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class PassKnifeRangeFeature
    {
        [ConfigField(224f, "递交武器可触及的最大距离，游戏默认为 224。")]
        public static ConfigEntry<float> Range;

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance, ref Player __result)
        {
            if (!FeatureGate.Enabled(typeof(PassKnifeRangeFeature)))
                return true;

            Player best = null;
            float bestDist = float.PositiveInfinity;
            float range = Range.Value;

            foreach (Player player in Managers.Player.Players.Values)
            {
                if (player.State == EPlayerState.Hide)
                    continue;
                if (Managers.Player.KnownBlackIds.Contains(player.PublicInfo.PlayerId))
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
