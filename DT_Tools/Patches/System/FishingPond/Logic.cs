using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Patches.System.FishingPond
{
    /// <summary>
    /// 完成水下捕捉任务的收口调用。MissionManager 在发行程序集中为 internal
    /// （0.1.15b MissionManager.cs:10），反射访问统一收敛到 Game/MissionAccess
    /// （ClearMission(ESchoolMission, Player, bool)：0.1.15b MissionManager.cs:437）。
    /// </summary>
    internal static class FishingPondLogic
    {
        public static void ClearMission(Server.Game.Player player, bool isInfected)
        {
            if (MissionAccess.ClearMission(ESchoolMission.ScAquaticCapture, player, isInfected))
                return;
            Log.Error<FishingPondFeature>("Server.Game.MissionManager.ClearMission 不可用（反射链路缺失或调用失败）");
        }
    }
}
