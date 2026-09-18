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

        public static AgentRunner Instance { get; private set; }
        private WebConsole _console;
        private AgentFilter _filter;
        private float _nextTick;
        private int _idle, _ticks, _done;

        public static void Start(WebConsole console, AgentFilter filter)
        {
            if (Instance != null)
            {
                Instance._filter = filter;
                Instance._idle = 0;
                Instance._ticks = 0;
                console.Log("[特工] 已在运行，已更新过滤并继续。", LogLevel.Info);
                return;
            }
            var go = new GameObject("DT_AgentRunner");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AgentRunner>();
            Instance._console = console;
            Instance._filter = filter;
            Instance._nextTick = Time.unscaledTime + 0.1f;

            string desc = filter.MissionId.HasValue ? $"任务 ScId={filter.MissionId}"
                : filter.DeviceId.HasValue ? $"设备 #{filter.DeviceId}"
                : "全清";
            console.Log($"[特工] 启动（{desc}），tick={TickInterval}s。一步做不了会跳过。/agent stop 停止。",
                LogLevel.Message);
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

            _idle = 0;
            ExecuteBatch(batch);
        }

        /// <summary>
        /// 单 tick 内批量执行：
        ///   · ChangesHand==false 的步骤（状态机推进/无道具直清）互不干扰，全部执行；
        ///   · ChangesHand==true 的步骤（进/出/替换 Hand）本 tick 最多执行 1 个——
        ///     依据 Server.Game/ItemManager.cs InsertInven 在 Hand 非空时会强制 DropItem
        ///     踢落已持物品（105-108 行），同 tick 连发两个改 Hand 包会导致先发的被踢落地。
        /// batch 已按 Priority 升序排列，故第一个遇到的改 Hand 步骤即为优先级最高者。
        /// </summary>
        private void ExecuteBatch(List<AgentStep> batch)
        {
            bool handStepTaken = false;
            foreach (var step in batch)
            {
                if (step.ChangesHand)
                {
                    if (handStepTaken) continue;
                    handStepTaken = true;
                }

                try
                {
                    step.Send();
                    _done++;
                    _console?.Log($"[特工] ({_done}) {step.Label}", LogLevel.Message);
                }
                catch (Exception ex)
                {
                    _console?.Log($"[特工] 异常: {ex.Message}", LogLevel.Error);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
