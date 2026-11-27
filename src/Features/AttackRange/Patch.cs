// 作用：为黑方攻击距离挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Features.AttackRange
{
    [HarmonyPatch(typeof(MyPlayer), "GetTargetPlayer")]
    internal static class AttackRangePatch
    {
        private static bool Prefix(MyPlayer __instance, ref Player __result)
        {
            if (!Engine.Enabled<AttackRangeFeature>())
                return true;

            if (__instance.Inventory.Weapon.DataId == 0)
            {
                __result = null;
                return false;
            }

            __result = NearestTargetFinder.Find(__instance.Position, AttackRangeFeature.Range);
            return false;
        }
    }
}
