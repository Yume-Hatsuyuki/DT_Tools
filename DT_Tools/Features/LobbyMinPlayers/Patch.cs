using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.LobbyMinPlayers
{
    [HarmonyPatch(typeof(Define), nameof(Define.LOBBY_MIN_PLAYER), MethodType.Getter)]
    internal static class LobbyMinPlayersPatch
    {
        private static bool Prefix(ref int __result)
        {
            if (!Engine.Enabled<LobbyMinPlayersFeature>())
                return true;

            if (Define.IsPlaytestApp)
                return true;

            __result = LobbyMinPlayersFeature.Value;
            return false;
        }
    }
}
