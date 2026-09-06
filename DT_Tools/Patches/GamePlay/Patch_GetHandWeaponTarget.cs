using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   MyPlayer::GetHandWeaponTarget()
    ///
    /// <b>原版效果</b>：
    ///   排除 Hide 与 KnownBlackIds；magnitude &gt; 224f 时跳过；射线遮挡 + 最近目标。
    ///
    /// <b>修改后效果</b>：
    ///   距离阈值改为 [GetHandWeaponTarget].Range（默认 224）。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版循环重写，仅替换 224f。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "GetHandWeaponTarget")]
    [PatchConfig(
        "Enable_GetHandWeaponTarget",
        "递刀距离：可在下方配置段修改最大递交距离（默认 224）。",
        author: "梦初雪")]
    internal static class Patch_GetHandWeaponTarget
    {
        private static ConfigEntry<float> _range;

        static Patch_GetHandWeaponTarget()
        {
            _range = Plugin.Instance.Config.Bind(
                "GetHandWeaponTarget",
                "Range",
                224f,
                new ConfigDescription("递交道具可触及的最大距离，游戏默认为 224。"));
        }

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance, ref Player __result)
        {
            Player best = null;
            float bestDist = float.PositiveInfinity;
            float range = _range.Value;

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

                LayerMask layerMask = 12288;
                if (!Physics2D.Raycast(__instance.Position, direction, magnitude, layerMask) &&
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
