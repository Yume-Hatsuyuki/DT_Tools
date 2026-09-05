using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Protocol;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：Player MyPlayer::GetHandWeaponTarget()
    /// 黑方递刀距离加长（支持配置）
    /// 原逻辑：224f 范围内寻找目标（排除已知黑方）
    /// 补丁逻辑：替换 GetHandWeaponTarget 的寻人范围，从 .cfg 读取自定义距离，默认 224f
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "GetHandWeaponTarget")]
    [PatchConfig("Enable_Patch_PassRange", "黑幕递刀距离加长：从 .cfg [BlackAttackRange] PassRange 读取自定义距离，游戏默认值为 224。", author: "梦初雪")]
    internal static class Patch_PassRange
    {
        private static ConfigEntry<float> _passRange;

        static Patch_PassRange()
        {
            var cfg = Plugin.Instance.Config;
            _passRange = cfg.Bind(
                "BlackAttackRange",
                "PassRange",
                224f,
                new ConfigDescription("黑幕递刀（GetHandWeaponTarget）的最大递刀距离，游戏默认值为 224")
            );
        }

        [HarmonyPrefix]
        private static bool Prefix(MyPlayer __instance, ref Player __result)
        {
            Player best = null;
            float bestDist = float.PositiveInfinity;
            float range = _passRange.Value;

            foreach (Player player in Managers.Player.Players.Values)
            {
                if (player.State == EPlayerState.Hide)
                    continue;

                if (Managers.Player.KnownBlackIds.Contains(player.PublicInfo.PlayerId))
                    continue;

                Vector2 delta = player.Position - __instance.Position;
                float magnitude = delta.magnitude;

                if (magnitude > range)
                    continue;

                LayerMask layerMask = 12288;
                if (!Physics2D.Raycast(__instance.Position, delta, magnitude, layerMask) && magnitude < bestDist)
                {
                    best = player;
                    bestDist = magnitude;
                }
            }

            __result = best;
            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}
