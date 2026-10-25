using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.BlackKillNotify
{
    [HarmonyPatch(typeof(Server.Game.Player), "OnDeadMurder")]
    internal static class BlackKillNotifyServerPatch
    {
        private static void Postfix(Server.Game.Player __instance, Server.Game.Player black)
        {
            if (!Engine.Enabled<BlackKillNotifyFeature>() || !BlackKillNotifyFeature.ServerSide)
                return;
            BlackKillNotifyLogic.NotifyFromHost(__instance, black);
        }
    }
}
