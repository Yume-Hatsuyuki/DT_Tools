using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.RemoveFog
{
    [HarmonyPatch(typeof(MyPlayer), "OnGhostVisualChanged")]
    internal static class RemoveFogPatch
    {
        private static bool Prefix()
        {
            if (!Engine.Enabled<RemoveFogFeature>())
                return true;

            Managers.Map.SetRoomShadowCasters(false);
            return false;
        }
    }
}
