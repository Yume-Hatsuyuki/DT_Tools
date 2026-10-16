using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Features.FishingPond
{
    /// <summary>
    /// 完成水下捕捉任务的收口调用。
    /// </summary>
    /// <remarks>
    /// MissionManager 在发行程序集中为 internal
    /// （0.1.16b Server.Game/MissionManager.cs:10（待核对）），反射访问统一收敛到 Game/MissionAccess
    /// （ClearMission(ESchoolMission, Player, bool)：0.1.18c Server.Game/MissionManager.cs:437）。
    /// </remarks>
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
