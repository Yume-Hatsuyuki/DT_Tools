using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.AttackRange
{
    /// <summary>
    /// GetTargetPlayer 整替：无武器返回 null；最近玩家搜索见 NearestTargetFinder
    /// （骨架与 0.1.16a MyPlayer.cs:2162 一致，仅 224f → 可配置 Range）。
    /// 方法为 private，字符串定位：0.1.16a MyPlayer.cs:2162。
    /// 覆盖范围注意：0.1.16a 教程局重写后用独立的 TutorialMyPlayer
    /// （0.1.16a TutorialMyPlayer.cs:7，继承 Tutorial_Player），不继承 MyPlayer、
    /// 攻击不走 GetTargetPlayer，故本补丁不覆盖教学局，教学局内攻击距离仍为原版逻辑。
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
