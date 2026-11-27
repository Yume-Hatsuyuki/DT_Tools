// 作用：为递刀距离挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Features.PassKnifeRange
{
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
