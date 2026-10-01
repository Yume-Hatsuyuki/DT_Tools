using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// StartTrialResult 前缀（私有方法，字符串定位：0.1.15b Server.Game/TrialManager.cs:746）：
    /// 「投票任意黑方胜利」开启且最高票是任意活着的黑方时接管结算演出。差异点：
    /// 1. 广播包口径改为「凶手=被票出的黑方」（CatchId 与 BlackId 同填该玩家）——
    ///    客户端过场按 catchId == blackId 走「抓获处决」分支（0.1.15b UI_TrialEvent.cs:1214-1240），
    ///    不改口径会播「未能抓获」，与随后的白方胜利结算自相矛盾；
    /// 2. 「投中凶手」奖励发给投出该黑方的玩家（原版只认本庭绑定凶手，TrialManager.cs:750-755）；
    /// 3. RemainingExecute/RemainingId 恒 0、MastermindExecuted 恒 false——双重处决与主谋处决
    ///    是「抓到本庭凶手且主谋存活」的原生场景（TrialManager.cs:761-767），此处不适用。
    /// 未命中（平票/无人投票/投中白方、主谋或本庭绑定凶手）一律放行原版。
    /// </summary>
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
                .Field("_candidates")                                   // 0.1.15b Server.Game/TrialManager.cs:47
                .GetValue<List<Server.Game.TrialManager.Candidate>>();
            if (candidates != null)
            {
                foreach (Server.Game.TrialManager.Candidate candidate in candidates)
                {
                    if (candidate.VotedTargetId == catchId)
                        Server.Game.AwardManager.Instance.OnVotedBlack(candidate.Info.PlayerId);   // 0.1.15b Server.Game/AwardManager.cs:61
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
                Traverse.Create(__instance).Method("FinalizeTrialResult").GetValue();   // 0.1.15b Server.Game/TrialManager.cs:780
            });
            return false;
        }
    }
}
