using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Protocol;
using UnityEngine;

namespace DT_Tools.Console.Commands.Agent
{
    /// <summary>
    /// Unity MonoBehaviour：每 tick 执行 Planner 给出的步骤。
    ///
    /// 参数依据（已核实 Server / Server.Game 源码，2026-09 复核）：
    ///   · 全服务端代码中，JobSerializer.Push→Flush 是无限容量 FIFO，同步立即执行；
    ///     Replicator.All/Broadcast 同步立即发送；HostPacketGate 仅拦截"未绑定玩家会话"的包，
    ///     与包频率无关。未发现任何针对 C_INTERACT_*/C_HANDLE_* 的限频、去重或冷却判定。
    ///     故 TickInterval 下探到 0.25s 不会触发服务端限流。
    ///   · Mineral.HandleEvent 服务端零冷却，每次收到 C_HANDLE_MINERAL{IsSuccess=true}
    ///     即无条件 CreateAndDropItem + Broadcast(S_SPAWN_DEVICE)；MineralMineCooldown
    ///     纯粹是客户端侧"等网络回包 + Cache 刷新"的自保护，与服务器无关。
    ///   · MaxIdleTicks / MaxTicks 随 TickInterval 变化按时长等效换算，
    ///     以保持"空闲多久停止"和"最多运行多久"的实际秒数不变。
    /// </summary>
    internal sealed class AgentRunner : MonoBehaviour
    {
        public const float TickInterval = 0.25f;   // 原 0.6f；服务端无限频证据见上
        public const int   MaxIdleTicks = 29;       // 原 12(=7.2s) → 0.25s下 29 tick≈7.25s，等效不变
        public const int   MaxTicks     = 720;      // 原 300(=180s) → 0.25s下 720 tick=180s，等效不变

        /// <summary>
        /// 改 Hand 操作的跨 tick 冷却 tick 数。
        ///
        /// 背景：Managers.Player.MyPlayer.PublicInfo.HandItemId 只在客户端收到服务端
        /// S_ADD_ITEM/S_STATE 等回包后才会更新（见 PacketHandler.Handle_S_ADD_ITEM →
        /// Inventory.InsertHand），是异步确认的，不是发包后立即生效的本地状态。
        ///
        /// 0.6s 的旧 tick 下，这个网络往返延迟（通常 <200ms）天然小于一个 tick 周期，
        /// 问题被掩盖；换成 0.25s 后，若延迟接近/超过一个 tick，Planner 会在
        /// HandItemId 尚未刷新时误判"仍是空手"，对同一个地面物品重复发送
        /// C_ACQUIRE_ITEM，造成重复拾取/重复交付判定，表现为同一目标反复出现在日志里。
        ///
        /// 修复：任何 ChangesHand==true 的步骤执行后，在本冷却期内不再执行新的改 Hand
        /// 步骤（不止是采矿，覆盖交付/拾取/丢弃/生成全部四类），留出时间等待服务端回包
        /// 把本地 Hand 状态刷新到位。冷却期结束判据是"经过的 tick 数"而非"状态真的变了"，
        /// 因为若某次操作被服务端拒绝，等状态变化会永久卡死；用固定冷却更保守也更安全。
        /// </summary>
        private const int HandOpCooldownTicks = 2; // 0.25s×2=0.5s，与矿物冷却同量级

        /// <summary>
        /// 卡死检测阈值：同一个 label（同一设备同一动作）连续被执行这么多次，
        /// 判定为"重复推进但未真正改变游戏状态"，主动停止并报警。
        /// 这是 HandOpCooldown 修复之外的第二道防线：若未来某处判断条件有误导致
        /// 类似的原地打转，能在数秒内被发现，而不必等到 MaxTicks 硬顶（180s）才停止。
        /// </summary>
        private const int StuckRepeatThreshold = 8;

        /// <summary>
        /// 任务全部完成的判定阈值（百分比）。
        /// 依据 Server.Game/MissionManager.cs CheckAllClear：
        ///   (int)(CurrentPoint/GoalPoint*100) >= 100 时设置 AllClear=true。
        /// 客户端通过 S_MISSION_PROGRESS_PERCENT 收到同一个 Percent 值，
        /// 经 PacketHandler.Handle_S_MISSION_PROGRESS_PERCENT 转发为
        /// Managers.Game.OnBroadcastSceneEvent(ChangeMissionPercent, Percent) 场景事件。
        /// </summary>
        private const int AllClearPercent = 100;

        public static AgentRunner Instance { get; private set; }
        private WebConsole _console;
        private AgentFilter _filter;
        private float _nextTick;
        private int _idle, _ticks, _done;
        private int _handOpCooldown;
        private string _lastLabel;
        private int _lastLabelRepeat;
        private bool _subscribed;

        public static void Start(WebConsole console, AgentFilter filter)
        {
            if (Instance != null)
            {
                Instance._filter = filter;
                Instance._idle = 0;
                Instance._ticks = 0;
                Instance._lastLabel = null;
                Instance._lastLabelRepeat = 0;
                console.Log("[特工] 已在运行，已更新过滤并继续。", LogLevel.Info);
                return;
            }
            var go = new GameObject("DT_AgentRunner");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AgentRunner>();
            Instance._console = console;
            Instance._filter = filter;
            Instance._nextTick = Time.unscaledTime + 0.1f;
            Instance.TrySubscribeMissionPercent();

            string desc = filter.MissionId.HasValue ? $"任务 ScId={filter.MissionId}"
                : filter.DeviceId.HasValue ? $"设备 #{filter.DeviceId}"
                : "全清";
            console.Log($"[特工] 启动（{desc}），tick={TickInterval}s。一步做不了会跳过。/agent stop 停止。",
                LogLevel.Message);
        }

        /// <summary>
        /// 订阅任务进度场景事件，用于检测"全部任务已完成"（见 AllClearPercent 依据）。
        ///
        /// 注意：Managers.Game.OnBroadcastSceneEvent 在每局游戏结束/重开时会被
        /// GameManagerEX.Clear() 整体置 null（见 Managers.Clear() 调用链），不是移除
        /// 单个订阅者，所以旧局的订阅不会自动带到新局——必须在每次 Runner 创建时
        /// 重新订阅，不能假设订阅一次全局有效。退订统一放在 OnDestroy 里。
        /// </summary>
        private void TrySubscribeMissionPercent()
        {
            if (_subscribed || Managers.Game == null) return;
            Managers.Game.OnBroadcastSceneEvent += OnSceneEvent;
            _subscribed = true;
        }

        private void OnSceneEvent(Define.ESceneEventType type, int value1, int value2)
        {
            if (type != Define.ESceneEventType.ChangeMissionPercent) return;
            if (value1 < AllClearPercent) return;

            _console?.Log(
                $"[特工] 检测到任务进度已达 {value1}%，判定全部完成，停止（完成 {_done} 步 / {_ticks} tick）。",
                LogLevel.Message);
            Destroy(gameObject);
            Instance = null;
        }

        public static void Stop(WebConsole console)
        {
            if (Instance == null)
            {
                console.Log("[特工] 未在运行。", LogLevel.Info);
                return;
            }
            console.Log($"[特工] 已停止（完成 {Instance._done} 步）。", LogLevel.Message);
            Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + TickInterval;
            _ticks++;

            if (Managers.Game == null || Managers.Game.State != EGameState.Survive)
            {
                _console?.Log("[特工] 离开生存阶段，停止。", LogLevel.Warning);
                Stop(_console);
                return;
            }
            if (!_subscribed) TrySubscribeMissionPercent();

            if (_handOpCooldown > 0) _handOpCooldown--;

            var batch = AgentPlanner.NextBatch(_filter);
            if (batch == null || batch.Count == 0)
            {
                _idle++;
                if (_idle >= MaxIdleTicks || _ticks >= MaxTicks)
                {
                    _console?.Log($"[特工] 结束：空闲 {_idle}，执行 {_done} 步 / {_ticks} tick。", LogLevel.Message);
                    Destroy(gameObject);
                    Instance = null;
                }
                return;
            }

            // 冷却期内若整批步骤全是改 Hand 类（没有可执行的 Instant），
            // 视为本 tick 无进展，计入空闲，避免死等期间 idle 卡在 0 而跑满 MaxTicks。
            bool anyExecuted = ExecuteBatch(batch);
            _idle = anyExecuted ? 0 : _idle + 1;
            if (_idle >= MaxIdleTicks || _ticks >= MaxTicks)
            {
                _console?.Log($"[特工] 结束：空闲 {_idle}，执行 {_done} 步 / {_ticks} tick。", LogLevel.Message);
                Destroy(gameObject);
                Instance = null;
            }
        }

        /// <summary>
        /// 单 tick 内批量执行：
        ///   · ChangesHand==false 的步骤（状态机推进/无道具直清）互不干扰，全部执行；
        ///   · ChangesHand==true 的步骤（进/出/替换 Hand）仅在 <see cref="_handOpCooldown"/>
        ///     归零时才执行，且本 tick 最多执行 1 个，执行后重新进入
        ///     <see cref="HandOpCooldownTicks"/> tick 冷却——
        ///     依据 1：Server.Game/ItemManager.cs InsertInven 在 Hand 非空时会强制
        ///     DropItem 踢落已持物品，同 tick 连发两个改 Hand 包会导致先发的被踢落地；
        ///     依据 2：HandItemId 的刷新依赖服务端回包（异步），冷却期给回包留出时间，
        ///     避免本地状态未及时刷新导致 Planner 对同一目标重复发送改 Hand 请求。
        /// batch 已按 Priority 升序排列，故第一个遇到的改 Hand 步骤即为优先级最高者。
        /// 返回值：本 tick 是否至少执行了一个步骤（供 idle 计数使用）。
        /// </summary>
        private bool ExecuteBatch(List<AgentStep> batch)
        {
            bool executedAny = false;
            bool handStepTaken = false;
            bool handOpAvailable = _handOpCooldown <= 0;

            foreach (var step in batch)
            {
                if (step.ChangesHand)
                {
                    if (!handOpAvailable || handStepTaken) continue;
                    handStepTaken = true;
                    _handOpCooldown = HandOpCooldownTicks;

                    // 卡死检测：仅针对改 Hand 类步骤，因为它们理应每次执行都推进游戏状态
                    // （拾取后物品消失、交付后目标清空等）。Instant 类允许重复触发
                    // （如矿工需要连续拉杆两次），不纳入本检测。
                    if (step.Label == _lastLabel)
                    {
                        _lastLabelRepeat++;
                        if (_lastLabelRepeat >= StuckRepeatThreshold)
                        {
                            _console?.Log(
                                $"[特工] 检测到卡死：「{step.Label}」连续 {_lastLabelRepeat} 次未推进，已停止。请检查该任务链判断逻辑。",
                                LogLevel.Error);
                            Destroy(gameObject);
                            Instance = null;
                            return executedAny;
                        }
                    }
                    else
                    {
                        _lastLabel = step.Label;
                        _lastLabelRepeat = 1;
                    }
                }

                try
                {
                    step.Send();
                    _done++;
                    executedAny = true;
                    _console?.Log($"[特工] ({_done}) {step.Label}", LogLevel.Message);
                }
                catch (Exception ex)
                {
                    _console?.Log($"[特工] 异常: {ex.Message}", LogLevel.Error);
                }
            }
            return executedAny;
        }

        private void OnDestroy()
        {
            if (_subscribed && Managers.Game != null)
                Managers.Game.OnBroadcastSceneEvent -= OnSceneEvent;
            _subscribed = false;
            if (Instance == this) Instance = null;
        }
    }
}
