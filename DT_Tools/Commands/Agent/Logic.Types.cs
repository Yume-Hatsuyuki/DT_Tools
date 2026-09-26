using System;

namespace DT_Tools.Commands.Agent
{
    // ═══════════════════════════════════════════════════
    //  /agent 计划步骤的支撑类型（对应旧 AgentTypes.cs）
    // ═══════════════════════════════════════════════════

    /// <summary>步骤优先级：数字越小越先做。</summary>
    internal static class Pri
    {
        public const int Vacuum     = 10;  // 获取仓储/地面
        public const int Instant    = 20;  // 无道具直清
        public const int Deliver    = 30;  // 手里有货交付
        public const int Generate   = 40;  // 假人/采矿/取电
        public const int TimedStart = 50;  // 浇水/充电放入/制冰启动（之后要等）
        public const int ShakeLast  = 90;  // 摇酒相关最后
    }

    /// <summary>任务过滤：按任务 ID（ESchoolMission 底层值）或设备 ID。</summary>
    internal struct AgentFilter
    {
        public int? MissionId;
        public int? DeviceId;
        public static AgentFilter ByMission(int id) => new AgentFilter { MissionId = id };
        public static AgentFilter ByDevice(int id) => new AgentFilter { DeviceId = id };
    }

    /// <summary>一条可执行步骤：Send 闭包发包，Runner 每 tick 批量执行。</summary>
    internal struct AgentStep
    {
        public int Priority;
        public string Label;
        public Action Send;

        /// <summary>
        /// 是否改变 Hand 状态（进手/出手/替换手上物品）。
        /// 依据：Server.Game/ItemManager.cs InsertInven（0.1.15b:101）在 Hand 非空时会强制
        /// DropItem 踢落已持物品（见 105-108 行），故同一 tick 内改 Hand 的步骤最多执行 1 个。
        /// 不改 Hand 的步骤（状态机推进、无道具直清）互不干扰，可在同一 tick 全部执行。
        /// </summary>
        public bool ChangesHand;
    }

    /// <summary>
    /// 步骤收集委托。changesHand 默认 false（不改变 Hand），与拆分前的调用点保持兼容——
    /// 仅在 Runner 引入"每 tick 多步"后才需要各 Add() 调用显式标注 true。
    /// </summary>
    internal delegate void AddDel(int priority, string label, int? mission, int deviceId,
        Action send, bool changesHand = false);
}
