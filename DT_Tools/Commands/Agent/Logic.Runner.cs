using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.Agent
{
    /// <summary>
    /// Unity MonoBehaviour：每 tick 执行 Planner 给出的步骤（对应旧 AgentRunner.cs）。
    /// 日志全部经启动命令的 CommandContext（ctx.Reply/Warn）输出，不直接使用 Log 门面。
    ///
    /// 参数依据（已核实 Server / Server.Game 源码，2026-09 复核）：
    ///   · 全服务端代码中，JobSerializer.Push→Flush 是无限容量 FIFO，同步立即执行；
    ///     Replicator.All/Broadcast 同步立即发送；HostPacketGate 仅拦截"未绑定玩家会话"的包，
    ///     与包频率无关。未发现任何针对 C_INTERACT_*/C_HANDLE_* 的限频、去重或冷却判定。
    ///     故 TickInterval 下探到 0.25s 不会触发服务端限流。
    ///   · Mineral.HandleEvent 服务端零冷却，每次收到 C_HANDLE_MINERAL{IsSuccess=true}
    ///     即无条件 CreateAndDropItem + Broadcast(S_SPAWN_DEVICE)（0.1.16b Server.Game/Mineral.cs:26/43）；
    ///     MineralMineCooldown 纯粹是客户端侧"等网络回包 + Cache 刷新"的自保护，与服务器无关。
    ///   · MaxIdleTicks / MaxTicks 随 TickInterval 变化按时长等效换算，
    ///     以保持"空闲多久停止"和"最多运行多久"的实际秒数不变。
    /// </summary>
    internal sealed class AgentRunner : MonoBehaviour
    {
        public const float TickInterval = 0.25f;   // 原 0.6f；服务端无限频证据见上
        public const int   MaxIdleTicks = 29;       // 原 12(=7.2s) → 0.25s下 29 tick≈7.25s，等效不变
        public const int   MaxTicks     = 720;      // 原 300(=180s) → 0.25s下 720 tick=180s，等效不变

        /// <summary>
        /// 改 Hand 操作的跨 tick 冷却（0.25s×2=0.5s）。依据：HandItemId 只在收到服务端
        /// S_ADD_ITEM/S_STATE 回包后更新（PacketHandler.Handle_S_ADD_ITEM → Inventory.InsertHand，
        /// 异步确认）；0.25s tick 下网络往返可能横跨 tick，Planner 会对同一目标重复发包。
        /// 改 Hand 步骤执行后冷却期内不再执行新的改 Hand 步骤（覆盖交付/拾取/丢弃/生成）；
        /// 判据用"经过 tick 数"而非"状态已变"——被服务端拒绝时等状态会永久卡死，固定冷却更保守。
        /// </summary>
        private const int HandOpCooldownTicks = 2;

        /// <summary>
        /// 卡死检测阈值：同一 label 连续执行这么多次即判"原地打转"，停止并报警。
        /// HandOpCooldown 之外的第二道防线，数秒内发现判断条件失效，而非等 MaxTicks 硬顶。
        /// </summary>
        private const int StuckRepeatThreshold = 8;

        /// <summary>
        /// 任务全部完成判定阈值。依据 Server.Game/MissionManager.cs CheckAllClear：
        /// Percent &gt;= 100 置 AllClear（0.1.16b :30 / :461）；客户端经
        /// S_MISSION_PROGRESS_PERCENT → 场景事件 ChangeMissionPercent 收到同一值
        /// （0.1.16b GameManagerEX.cs:258、Define.cs:170）。
        /// </summary>
        private const int AllClearPercent = 100;

        public static AgentRunner Instance { get; private set; }
        private CommandContext _ctx;
        private AgentFilter _filter;
        private float _nextTick;
        private int _idle, _ticks, _done;
        private int _handOpCooldown;
        private string _lastLabel;
        private int _lastLabelRepeat;
        private bool _subscribed;

        public static void Start(CommandContext ctx, AgentFilter filter)
        {
            if (Instance != null)
            {
                Instance._filter = filter;
                Instance._idle = 0;
                Instance._ticks = 0;
                Instance._lastLabel = null;
                Instance._lastLabelRepeat = 0;
                ctx.Reply("[特工] 已在运行，已更新过滤并继续。");
                return;
            }
            var go = new GameObject("DT_AgentRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<AgentRunner>();
            Instance._ctx = ctx;
            Instance._filter = filter;
            Instance._nextTick = Time.unscaledTime + 0.1f;
            Instance.TrySubscribeMissionPercent();

            string desc = filter.MissionId.HasValue ? $"任务 ScId={filter.MissionId}"
                : filter.DeviceId.HasValue ? $"设备 #{filter.DeviceId}"
                : "全清";
            ctx.Reply($"[特工] 启动（{desc}），tick={TickInterval}s。一步做不了会跳过。/agent stop 停止。");
        }

        /// <summary>
        /// 订阅任务进度场景事件，用于检测"全部任务已完成"（见 AllClearPercent 依据）。
        ///
        /// 注意：Managers.Game.OnBroadcastSceneEvent 在每局游戏结束/重开时会被
        /// GameManagerEX.Clear() 整体置 null（0.1.16b GameManagerEX.cs:280，Managers.Clear() 调用链），
        /// 不是移除单个订阅者，所以旧局的订阅不会自动带到新局——必须在每次 Runner 创建时
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

            _ctx?.Reply(
                $"[特工] 检测到任务进度已达 {value1}%，判定全部完成，停止（完成 {_done} 步 / {_ticks} tick）。");
            Destroy(gameObject);
            Instance = null;
        }

        public static void Stop(CommandContext ctx)
        {
            if (Instance == null)
            {
                ctx.Reply("[特工] 未在运行。");
                return;
            }
            ctx.Reply($"[特工] 已停止（完成 {Instance._done} 步）。");
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
                _ctx?.Warn("[特工] 离开生存阶段，停止。");
                Stop(_ctx);
                return;
            }
            if (!_subscribed) TrySubscribeMissionPercent();

            if (_handOpCooldown > 0) _handOpCooldown--;

            var batch = AgentPlanner.NextBatch(_filter);
            if (batch == null || batch.Count == 0)
            {
                _idle++;
                if (_idle >= MaxIdleTicks || _ticks >= MaxTicks)
                    Finish();
                return;
            }

            // 冷却期内若整批步骤全是改 Hand 类（没有可执行的 Instant），
            // 视为本 tick 无进展，计入空闲，避免死等期间 idle 卡在 0 而跑满 MaxTicks。
            bool anyExecuted = ExecuteBatch(batch);
            _idle = anyExecuted ? 0 : _idle + 1;
            if (_idle >= MaxIdleTicks || _ticks >= MaxTicks)
                Finish();
        }

        /// <summary>
        /// 单 tick 内批量执行：
        ///   · ChangesHand==false 的步骤（状态机推进/无道具直清）互不干扰，全部执行；
        ///   · ChangesHand==true 的步骤（进/出/替换 Hand）仅在 <see cref="_handOpCooldown"/>
        ///     归零时才执行，且本 tick 最多执行 1 个，执行后重新进入
        ///     <see cref="HandOpCooldownTicks"/> tick 冷却——
        ///     依据 1：Server.Game/ItemManager.cs InsertInven（0.1.16b:101）在 Hand 非空时会强制
        ///     DropItem 踢落已持物品（:105-108），同 tick 连发两个改 Hand 包会导致先发的被踢落地；
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
                            _ctx?.Warn(
                                $"[特工] 检测到卡死：「{step.Label}」连续 {_lastLabelRepeat} 次未推进，已停止。请检查该任务链判断逻辑。");
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
                    _ctx?.Reply($"[特工] ({_done}) {step.Label}");
                }
                catch (Exception ex)
                {
                    _ctx?.Warn($"[特工] 异常: {ex.Message}");
                }
            }
            return executedAny;
        }

        private void Finish()
        {
            _ctx?.Reply($"[特工] 结束：空闲 {_idle}，执行 {_done} 步 / {_ticks} tick。");
            Destroy(gameObject);
            Instance = null;
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
