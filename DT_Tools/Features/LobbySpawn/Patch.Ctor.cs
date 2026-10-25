using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.LobbySpawn
{
    [HarmonyPatch(typeof(Server.Game.Player), MethodType.Constructor,
        new[] { typeof(int), typeof(HostPeerSession), typeof(string) })]
    internal static class LobbySpawnCtorPatch
    {
        private static void Postfix(Server.Game.Player __instance)
        {
            if (!Engine.Enabled<LobbySpawnFeature>())
                return;
            if (__instance?.PublicInfo?.Pos == null)
                return;
            if (!LobbySpawnLogic.TryResolve(__instance.AccountID, out PosInfo pos))
                return;

            __instance.PublicInfo.Pos.X = pos.X;
            __instance.PublicInfo.Pos.Y = pos.Y;
            __instance.EffectivePosition.X = pos.X;
            __instance.EffectivePosition.Y = pos.Y;
            Log.Info<LobbySpawnFeature>($"{__instance.Name} 入场位置已覆盖（{pos.X:0}, {pos.Y:0}）。");
        }
    }
}
