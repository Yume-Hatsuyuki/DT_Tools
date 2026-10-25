using System;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    internal static class AgentPlanner
    {
        public static List<string> Peek(AgentFilter filter) =>
            PlanAll(filter).Select(s => s.Label).ToList();

        public static List<AgentStep> NextBatch(AgentFilter filter) => PlanAll(filter);

        private static List<AgentStep> PlanAll(AgentFilter filter)
        {
            var steps = new List<AgentStep>();
            int handRaw = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
            int hand = handRaw > 0 ? handRaw : 0;
            var devices = Managers.Device.Cache.Values
                .Where(d => d?.Info != null && (d.Data != null || d.DeviceType == EDeviceType.ItemHolder))
                .ToList();

            void Add(int priority, string label, int? mission, int deviceId, Action send, bool changesHand = false)
            {
                if (filter.MissionId.HasValue && mission.HasValue && mission.Value != filter.MissionId.Value)
                    return;
                if (filter.DeviceId.HasValue && deviceId != 0 && deviceId != filter.DeviceId.Value)
                    return;

                string display = mission.HasValue
                    ? $"[P{priority}] ID {mission.Value} · {label}"
                    : $"[P{priority}] ID - · {label}";
                steps.Add(new AgentStep
                {
                    Priority = priority,
                    Label = display,
                    Send = send,
                    ChangesHand = changesHand
                });
            }

            if (hand > 0 && AgentItemHelper.ShouldDropHand(hand, devices))
            {
                Add(Pri.Vacuum - 5, $"丢弃手上{hand}", null, 0, () => AgentItemHelper.TryDropHand(),
                    changesHand: true);
                return steps.OrderBy(s => s.Priority).ToList();
            }

            if (hand > 0)
                AgentDeliveryChain.Collect(hand, devices, Add);

            if (hand == 0)
                AgentVacuumChain.Collect(devices, filter, Add);

            AgentInstantChain.Collect(devices, Add);

            if (hand == 0)
                AgentGenerateChain.Collect(devices, filter, Add);

            return steps.OrderBy(s => s.Priority).ToList();
        }
    }
}
