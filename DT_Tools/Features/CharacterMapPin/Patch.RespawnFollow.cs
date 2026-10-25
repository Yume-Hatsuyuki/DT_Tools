using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.CharacterMapPin
{
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.HandleRespawn))]
    internal static class CharacterMapPinKeepDeadPinFollowPatch
    {
        private static void Postfix(S_RESPAWN pkt)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.KeepDeadPin)
                return;

            MyPlayer my = Managers.Player.MyPlayer;
            if (my == null || pkt.PlayerId == my.PublicInfo.PlayerId)
                return;

            if (!CharacterMapPinState.KeptPinIds.Contains(pkt.PlayerId))
                return;

            CharacterMapPinLogic.FollowKeptPin(pkt.PlayerId, pkt.Pos);
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.Spawn), new[] { typeof(PublicPlayerInfo) })]
    internal static class CharacterMapPinKeepDeadPinSpawnCleanupPatch
    {
        private static void Postfix(PublicPlayerInfo info)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.KeepDeadPin)
                return;

            CharacterMapPinState.KeptPinIds.Remove(info.PlayerId);
        }
    }
}
