using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.LobbySpawn
{
    [HarmonyPatch(typeof(Server.Game.Player), nameof(Server.Game.Player.Move))]
    internal static class LobbySpawnMovePatch
    {
        private static void Prefix(Server.Game.Player __instance, ref PosInfo pos)
        {
            if (!Engine.Enabled<LobbySpawnFeature>())
                return;
            if (__instance == null || pos == null || Managers.Data?.MapData == null)
                return;
            if (!ReferenceEquals(pos, Managers.Data.MapData.LobbyPos))
                return;
            if (LobbySpawnLogic.TryResolve(__instance.AccountID, out PosInfo target))
                pos = target;
        }
    }
}
