using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.PassKnifeRange
{
    /// <summary>
    /// GetHandWeaponTarget 整替：最近玩家搜索见 NearestTargetFinder（骨架与
    /// 0.1.15b MyPlayer.cs:2191 一致），额外排除 KnownBlackIds
    /// （0.1.15b MyPlayer.cs:2197 原文内联于循环，此处收敛为谓词）。
    /// 方法为 private，字符串定位：0.1.15b MyPlayer.cs:2191。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "GetHandWeaponTarget")]
    internal static class PassKnifeRangePatch
    {
        private static bool Prefix(MyPlayer __instance, ref Player __result)
        {
            if (!Engine.Enabled<PassKnifeRangeFeature>())
                return true;

            __result = NearestTargetFinder.Find(
                __instance.Position,
                PassKnifeRangeFeature.Range,
                player => !Managers.Player.KnownBlackIds.Contains(player.PublicInfo.PlayerId));
            return false;
        }
    }
}
