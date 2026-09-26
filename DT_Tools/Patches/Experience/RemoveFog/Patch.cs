using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.RemoveFog
{
    /// <summary>
    /// 原版 OnGhostVisualChanged：SetRoomShadowCasters(state != 1)，整替为恒 false。
    /// 方法为 protected override，字符串定位：0.1.15b MyPlayer.cs:1889。
    /// </summary>
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
