using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.Agent
{
    internal sealed class AgentRunner : MonoBehaviour
    {
        public const float TickInterval = 0.25f;
        public const int   MaxIdleTicks = 29;
        public const int   MaxTicks     = 720;

        private const int HandOpCooldownTicks = 2;

        private const int StuckRepeatThreshold = 8;

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

            bool anyExecuted = ExecuteBatch(batch);
            _idle = anyExecuted ? 0 : _idle + 1;
            if (_idle >= MaxIdleTicks || _ticks >= MaxTicks)
                Finish();
        }

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
