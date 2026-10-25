using System;

namespace DT_Tools.Commands.Agent
{

    internal static class Pri
    {
        public const int Vacuum     = 10;
        public const int Instant    = 20;
        public const int Deliver    = 30;
        public const int Generate   = 40;
        public const int TimedStart = 50;
        public const int ShakeLast  = 90;
    }

    internal struct AgentFilter
    {
        public int? MissionId;
        public int? DeviceId;
        public static AgentFilter ByMission(int id) => new AgentFilter { MissionId = id };
        public static AgentFilter ByDevice(int id) => new AgentFilter { DeviceId = id };
    }

    internal struct AgentStep
    {
        public int Priority;
        public string Label;
        public Action Send;

        public bool ChangesHand;
    }

    internal delegate void AddDel(int priority, string label, int? mission, int deviceId,
        Action send, bool changesHand = false);
}
