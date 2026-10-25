using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CanShowLie
{
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
