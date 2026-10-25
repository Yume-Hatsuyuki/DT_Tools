using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Features.FishingPond
{
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
