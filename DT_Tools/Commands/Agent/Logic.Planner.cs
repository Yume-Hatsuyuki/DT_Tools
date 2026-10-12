using System;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    /// 任务规划器（对应旧 AgentPlanner.cs）。四条 Collect 链体位于独立文件
    /// （Logic.DeliveryChain / Logic.VacuumChain / Logic.InstantChain / Logic.GenerateChain）。
    ///
    /// PlanAll 返回并保留完整有序步骤列表。Runner 借此在同一 tick 内批量执行：
    /// 所有 ChangesHand==false 的步骤（互不干扰，可全部执行）+ 优先级最高的 1 个
    /// ChangesHand==true 的步骤（同 tick 最多 1 次，依据见 AgentStep.ChangesHand 注释）。
    internal static class AgentPlanner
    {
        public static List<string> Peek(AgentFilter filter) =>
            PlanAll(filter).Select(s => s.Label).ToList();

        /// 多步版本：供 Runner 在单 tick 内批量执行。已按 Priority 升序排列。
        public static List<AgentStep> NextBatch(AgentFilter filter) => PlanAll(filter);

        private static List<AgentStep> PlanAll(AgentFilter filter)
        {
            var steps = new List<AgentStep>();
            // 注意：原版空手时 HandItemId == -1，不是 0
            int handRaw = Managers.Player?.MyPlayer?.PublicInfo?.HandItemId ?? 0;
            int hand = handRaw > 0 ? handRaw : 0;
            // ItemHolder（地上掉落）为临时设备，常无 DeviceData → Data==null，不能过滤掉
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
                // 本 tick 只丢，不叠加其它依赖空手的步骤
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
