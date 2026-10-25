using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.MultiArmoryWeapon
{
    [HarmonyPatch(typeof(Server.Game.TrialManager), "StartTrialResult")]
    internal static class MultiArmoryWeaponStartTrialResultPatch
    {
        private static bool Prefix(Server.Game.TrialManager __instance)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>() || !MultiArmoryWeaponFeature.AnyBlackVoteWin)
                return true;

            if (!MultiArmoryWeaponLogic.TryGetAnyBlackCatch(__instance, out int catchId, out string skipReason))
            {
                Log.Debug<MultiArmoryWeaponFeature>($"投票任意黑方胜利未接管结算演出：{skipReason}");
                return true;
            }

            Server.Game.GameRoom room = __instance.Room;
            List<Server.Game.TrialManager.Candidate> candidates = Traverse.Create(__instance)
                .Field("_candidates")
                .GetValue<List<Server.Game.TrialManager.Candidate>>();
            if (candidates != null)
            {
                foreach (Server.Game.TrialManager.Candidate candidate in candidates)
                {
                    if (candidate.VotedTargetId == catchId)
                        Server.Game.AwardManager.Instance.OnVotedBlack(candidate.Info.PlayerId);
                }
            }

            S_RESULT_TRIAL packet = new S_RESULT_TRIAL
            {
                CatchId = catchId,
                BlackId = catchId,
                MastermindId = (room.MasterMind?.PublicInfo.PlayerId ?? 0),
                RemainingExecute = 0,
                RemainingId = 0,
                MastermindExecuted = false
            };
            Log.Info<MultiArmoryWeaponFeature>(
                $"投票任意黑方胜利：最高票 #{catchId}（黑方）按抓获口径结算演出，本庭绑定凶手 #{__instance.Black?.PublicInfo.PlayerId ?? 0} 仍存活");

            room.BroadcastPacketAndWaitResponse(packet, delegate
            {
                Traverse.Create(__instance).Method("FinalizeTrialResult").GetValue();
            });
            return false;
        }
    }
}
