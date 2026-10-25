using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.NicknameLimit
{
    [HarmonyPatch(typeof(UI_LobbyScene), "StartLobbySection")]
    internal static class NicknameLimitStartLobbyPatch
    {
        private static void Postfix(UI_LobbyScene __instance)
        {
            if (!Engine.Enabled<NicknameLimitFeature>())
                return;

            NicknameLimitLogic.RefreshScene(__instance, "StartLobbySection");
        }
    }
}
