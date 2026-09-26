using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.AttackRange
{
    /// <summary>
    /// GetTargetPlayer 整替：无武器返回 null；最近玩家搜索见 NearestTargetFinder
    /// （骨架与 0.1.15b MyPlayer.cs:2162 一致，仅 224f → 可配置 Range）。
    /// 方法为 private，字符串定位：0.1.15b MyPlayer.cs:2162。
    /// 覆盖范围注意：教学局用独立子类 RuleTutorial_MyPlayer（0.1.15b :7，继承 Tutorial_Player），
    /// 其私有 GetTargetPlayer（0.1.15b RuleTutorial_MyPlayer.cs:396）不在本补丁覆盖范围，
    /// 教学局副本内攻击距离仍为原版 224。
    /// </summary>
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
