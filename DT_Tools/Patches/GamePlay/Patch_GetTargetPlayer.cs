using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   MyPlayer::GetTargetPlayer()
    ///
    /// <b>原版效果</b>：
    ///   在 magnitude &gt; 224f 时跳过；其余逻辑为射线遮挡检测 + 最近目标。
    ///
    /// <b>修改后效果</b>：
    ///   距离阈值改为 [GetTargetPlayer].Range（默认 224）。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版循环重写，仅替换 224f。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "GetTargetPlayer")]
    [PatchConfig(
        "Enable_GetTargetPlayer",
        "黑方攻击距离：可在下方配置段修改最大攻击距离（默认 224）。",
        author: "梦初雪")]
    internal static class Patch_GetTargetPlayer
    {
        private static ConfigEntry<float> _range;

        static Patch_GetTargetPlayer()
        {
            _range = Plugin.Instance.Config.Bind(
                "GetTargetPlayer",
                "Range",
                224f,
                new ConfigDescription("黑方攻击可命中的最大距离，游戏默认为 224。"));
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
            float range = _range.Value;

            foreach (Player player in Managers.Player.Players.Values)
            {
                if (player.State == EPlayerState.Hide)
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
