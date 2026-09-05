using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Protocol;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：Player MyPlayer::GetTargetPlayer()
    /// 黑方刀人距离加长（支持配置）
    /// 原逻辑：224f 范围内寻找目标
    /// 补丁逻辑：替换 GetTargetPlayer 的寻敌范围，从 .cfg 读取自定义距离，默认 224f
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "GetTargetPlayer")]
    [PatchConfig("Enable_Patch_KillRange", "黑方刀人距离加长：替换 GetTargetPlayer 的寻敌范围，从 .cfg [BlackAttackRange] KillRange 读取自定义距离，游戏默认值为 224。", author: "梦初雪")]
    internal static class Patch_KillRange
    {
        private static ConfigEntry<float> _killRange;

        static Patch_KillRange()
        {
            var cfg = Plugin.Instance.Config;
            _killRange = cfg.Bind(
                "BlackAttackRange",
                "KillRange",
                224f,
                new ConfigDescription("黑方刀人（GetTargetPlayer）的最大攻击距离，游戏默认值为 224")
            );
        }

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
            float range = _killRange.Value;

            foreach (Player player in Managers.Player.Players.Values)
            {
                if (player.State == EPlayerState.Hide)
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
