using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CanShowLie
{
    /// IsMyPlayerBlack 整替：凡有本地玩家一律按黑幕处理，解锁伪证 UI（原版见 0.1.17a UI_GameTablet.cs:1005）。
    /// 私有方法，字符串定位。
    [HarmonyPatch(typeof(UI_GameTablet), "IsMyPlayerBlack")]
    internal static class CanShowLieBlackCheckPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<CanShowLieFeature>())
                return true;

            __result = Managers.Player.MyPlayer != null;
            return false;
        }
    }
}
