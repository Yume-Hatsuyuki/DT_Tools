using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.DtPresence
{
    [HarmonyPatch(typeof(DummyClient.SteamLobbyManager), "OnLobbyEnter")]
    internal static class DtPresenceLobbyEnterPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<DtPresenceFeature>())
                return;
            DtPresenceLogic.TryPublish("OnLobbyEnter");
        }
    }

    [HarmonyPatch]
    internal static class DtPresenceNotifyBlackPatch
    {
        internal static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_NOTIFY_BLACK");

        private static void Postfix()
        {
            if (!Engine.Enabled<DtPresenceFeature>() || !DtPresenceFeature.MarkOthers)
                return;
            DtPresenceLogic.Tick();
        }
    }
}
