using System.Collections.Generic;
using DT_Tools.Game;

namespace DT_Tools.Commands.Agent
{
    internal static class AgentMissionState
    {
        private static bool IsHost => HostGuard.IsHost;

        public static bool IsReady
        {
            get
            {
                if (IsHost)
                    return MissionAccess.Available && MissionAccess.Instance != null;
                return Server.Game.MissionMirror.HasState;
            }
        }

        public static HashSet<int> ActiveMissionTypes
        {
            get
            {
                if (IsHost)
                    return MissionAccess.CollectTypes(MissionAccess.ProgressList);
                return new HashSet<int>(Server.Game.MissionMirror.ProgressTypes);
            }
        }

        public static bool IsActive(int missionType)
        {
            if (!IsReady) return true;
            return ActiveMissionTypes.Contains(missionType);
        }
    }
}
