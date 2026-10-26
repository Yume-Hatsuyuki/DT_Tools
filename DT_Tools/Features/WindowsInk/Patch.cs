using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.WindowsInk
{
    [HarmonyPatch(typeof(Managers), "Update")]
    internal static class WindowsInkUpdatePatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<WindowsInkFeature>())
            {
                return;
            }
            PenMessageBridge.EnsureInstalled();
        }
    }
}
