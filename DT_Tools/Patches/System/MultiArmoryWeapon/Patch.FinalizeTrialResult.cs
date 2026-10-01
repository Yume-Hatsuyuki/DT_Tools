using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// FinalizeTrialResult 前缀（私有方法，字符串定位：0.1.15b Server.Game/TrialManager.cs:780）：
    /// 原版只有最高票 == 本庭绑定凶手才判白胜（TrialManager.cs:787-791），否则一律黑胜——
    /// 多刀场景下投中其他黑方会因此误判白方失败。命中「投票任意黑方胜利」时复刻原版白胜分支
    /// （ResultType=WhiteWin → ApplyTeamResults → TotalResult，TrialManager.cs:789-798）并跳过原版；
    /// 未命中（平票/无人投票/投中白方、主谋或本庭绑定凶手）放行原版。
    /// 判定与 StartTrialResult 补丁共用 MultiArmoryWeaponLogic.TryGetAnyBlackCatch，口径一致；
    /// 主机迁移恢复路径 ResumeSubstateAfterMigration 直达本方法（TrialManager.cs:952-954），同样被覆盖。
    /// </summary>
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
            room.ResultType = EResultType.WhiteWin;          // 0.1.15b Protocol/EResultType.cs:8
            room.ApplyTeamResults();                          // 0.1.15b Server.Game/GameRoom.cs:725（WhiteWin 分支不设 PrimaryWinnerId）
            Log.Info<MultiArmoryWeaponFeature>($"投票任意黑方胜利：最高票 #{catchId} 为黑方，判白方胜利");
            room.ChangeGameState(EGameState.TotalResult);     // 0.1.15b Server.Game/GameRoom.cs:573
            return false;
        }
    }
}
