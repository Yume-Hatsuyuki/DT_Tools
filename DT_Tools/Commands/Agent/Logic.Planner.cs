using System;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// 任务规划器（对应旧 AgentPlanner.cs）。四条 Collect 链体位于独立文件
    /// （Logic.DeliveryChain / Logic.VacuumChain / Logic.InstantChain / Logic.GenerateChain）。
    ///
    /// PlanAll 返回并保留完整有序步骤列表。Runner 借此在同一 tick 内批量执行：
    /// 所有 ChangesHand==false 的步骤（互不干扰，可全部执行）+ 优先级最高的 1 个
    /// ChangesHand==true 的步骤（同 tick 最多 1 次，依据见 AgentStep.ChangesHand 注释）。
    /// </summary>
    internal static class AgentPlanner
    {
        public static List<string> Peek(AgentFilter filter) =>
            PlanAll(filter).Select(s => s.Label).ToList();

        /// <summary>多步版本：供 Runner 在单 tick 内批量执行。已按 Priority 升序排列。</summary>
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

            // ── A0. 手上杂物（鱼等）先丢掉，避免卡住后续任务 ──
            if (hand > 0 && AgentItemHelper.ShouldDropHand(hand, devices))
            {
                Add(Pri.Vacuum - 5, $"丢弃手上{hand}", null, 0, () => AgentItemHelper.TryDropHand(),
                    changesHand: true);
                // 本 tick 只丢，不叠加其它依赖空手的步骤
                return steps.OrderBy(s => s.Priority).ToList();
            }

            // ── A. 手持 → 交付（高优先，清掉手上再干别的）──
            if (hand > 0)
                AgentDeliveryChain.Collect(hand, devices, Add);

            // ── B. 手空：顺手牵羊（仓储/地面）──
            if (hand == 0)
                AgentVacuumChain.Collect(devices, filter, Add);

            // ── C. 无道具直清 / 状态机 ──
            AgentInstantChain.Collect(devices, Add);

            // ── D. 手空：生成源（假人、采矿、取电、花…）──
            if (hand == 0)
                AgentGenerateChain.Collect(devices, filter, Add);

            return steps.OrderBy(s => s.Priority).ToList();
        }
    }
}
