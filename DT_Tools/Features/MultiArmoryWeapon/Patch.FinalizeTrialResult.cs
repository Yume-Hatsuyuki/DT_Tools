using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.MultiArmoryWeapon
{
    [HarmonyPatch(typeof(Server.Game.TrialManager), "FinalizeTrialResult")]
    internal static class MultiArmoryWeaponFinalizeTrialResultPatch
    {
        private static bool Prefix(Server.Game.TrialManager __instance)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>() || !MultiArmoryWeaponFeature.AnyBlackVoteWin)
                return true;

            if (!MultiArmoryWeaponLogic.TryGetAnyBlackCatch(__instance, out int catchId, out string skipReason))
            {
                Log.Debug<MultiArmoryWeaponFeature>($"投票任意黑方胜利未接管胜负：{skipReason}");
                return true;
            }

            Server.Game.GameRoom room = __instance.Room;
            room.ResultType = EResultType.WhiteWin;
            room.ApplyTeamResults();
            Log.Info<MultiArmoryWeaponFeature>($"投票任意黑方胜利：最高票 #{catchId} 为黑方，判白方胜利");
            room.ChangeGameState(EGameState.TotalResult);
            return false;
        }
    }
}
