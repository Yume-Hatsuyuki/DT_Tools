using System;
using System.Linq;
using BepInEx.Configuration;
using Protocol;
using Server.Game;
using DT_Tools.Automation.Shared;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Automation.AutoAcquireWeapon
{
    /// <summary>
    /// 进入 Survive 后自动从开放武器架取刀（C_INTERACT_ARMORY，无视距离）。
    /// 逻辑对齐 /acquire_weapon（天匠）：White→AcquireWeapon，Dark→SelectBlack。
    /// </summary>
    [AutomationModule(
        id: "acquire-weapon",
        section: "Auto.AcquireWeapon",
        displayName: "开局自动取刀",
        description: "进入生存阶段后自动对开放武器架发送 C_INTERACT_ARMORY（白方拔刀 / 黑幕截刀）。",
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal sealed class AutoAcquireWeaponModule : IAutomationModule
    {
        public string Id => "acquire-weapon";
        public string Section => "Auto.AcquireWeapon";
        public string DisplayName => "开局自动取刀";
        public string Description =>
            "进入生存阶段后自动对开放武器架发送 C_INTERACT_ARMORY（白方拔刀 / 黑幕截刀）。";
        public FeatureSide Side => FeatureSide.Client;
        public string Author => "梦初雪";

        public AutomationLogBuffer Log { get; } = new AutomationLogBuffer();

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> DelaySeconds;
        public static ConfigEntry<float> RetryInterval;
        public static ConfigEntry<int> MaxAttempts;

        public bool ModuleEnabled => Enabled != null && Enabled.Value;

        private bool _bound;
        private EGameState _lastState = EGameState.NoneState;
        private bool _doneThisRound;
        private float _phaseEnterRealtime = -1f;
        private float _nextTryRealtime;
        private int _attempts;

        public void BindConfig(ConfigFile config)
        {
            if (_bound) return;
            Enabled = config.Bind(
                Section,
                "Enabled",
                false,
                "是否启用：进入生存阶段后自动取刀。");
            DelaySeconds = config.Bind(
                Section,
                "DelaySeconds",
                30.0f,
                "首次尝试前等待（秒）。进入生存阶段以后多久开始拔刀。");
            RetryInterval = config.Bind(
                Section,
                "RetryInterval",
                1.0f,
                "重试间隔（秒）。刀架未开放 / 发包失败后隔多久再试。");
            MaxAttempts = config.Bind(
                Section,
                "MaxAttempts",
                50,
                "本局最多尝试次数。达到后停止。");
            _bound = true;
            Log.Info("配置已绑定");
        }

        public void Tick(bool hostEnabled)
        {
            if (!hostEnabled || !ModuleEnabled)
            {
                ResetTracking();
                return;
            }

            if (!TryGetState(out EGameState state))
                return;

            if (state != _lastState)
            {
                if (state == EGameState.Survive)
                {
                    _doneThisRound = false;
                    _phaseEnterRealtime = Time.realtimeSinceStartup;
                    _nextTryRealtime = 0f;
                    _attempts = 0;
                    Log.Info("已进入 Survive，准备自动取刀");
                }
                else if (_lastState == EGameState.Survive)
                {
                    Log.Info($"离开 Survive → {state}");
                    _doneThisRound = true;
                }
                _lastState = state;
            }

            if (state == EGameState.Survive && !_doneThisRound)
                TryAcquire();
        }

        private void TryAcquire()
        {
            float delay = DelaySeconds != null ? Mathf.Max(0f, DelaySeconds.Value) : 1f;
            if (_phaseEnterRealtime < 0f)
                _phaseEnterRealtime = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - _phaseEnterRealtime < delay)
                return;
            if (Time.realtimeSinceStartup < _nextTryRealtime)
                return;

            int max = MaxAttempts != null ? Math.Max(1, MaxAttempts.Value) : 45;
            if (_attempts >= max)
            {
                _doneThisRound = true;
                Log.Warn($"已达 MaxAttempts={max}，本局停止自动取刀");
                return;
            }

            var my = Managers.Player?.MyPlayer;
            if (my == null || my.PrivateInfo == null)
            {
                ScheduleRetry(0.5f);
                return;
            }

            // 已持刀 / 已是黑方：无需再取
            if (my.Color == EPlayerColor.Black
                || (my.Inventory != null && my.Inventory.Weapon.DataId != 0))
            {
                _doneThisRound = true;
                Log.Info($"已持刀或已是 Black（Color={my.Color}），跳过");
                return;
            }

            EArmoryInteractType interactType;
            switch (my.Color)
            {
                case EPlayerColor.White:
                    interactType = EArmoryInteractType.AcquireWeapon;
                    break;
                case EPlayerColor.Dark:
                    if (Managers.Game != null && Managers.Game.WeaponPickupLocked)
                    {
                        Log.Warn("WeaponPickupLocked，暂缓截刀");
                        ScheduleRetry();
                        return;
                    }
                    interactType = EArmoryInteractType.SelectBlack;
                    break;
                default:
                    _doneThisRound = true;
                    Log.Warn($"颜色 {my.Color} 不可取刀，停止");
                    return;
            }

            var open = Managers.Device?.Cache?.Values
                .Where(d => d != null
                            && d.DeviceType == EDeviceType.Armory
                            && d.DeviceState == (int)EArmoryState.OpenArmory)
                .OrderBy(d => d.ID)
                .ToList();

            if (open == null || open.Count == 0)
            {
                if (_attempts == 0 || _attempts % 5 == 0)
                    Log.Info("尚无开放武器架，等待刷新…");
                ScheduleRetry();
                return;
            }

            var armory = open[0];
            var packet = new C_INTERACT_ARMORY
            {
                ArmoryId = armory.ID,
                Type = interactType
            };
            _attempts++;
            if (!ClientPacket.TrySend(packet, out string err))
            {
                Log.Warn($"取刀发包失败: {err}");
                ScheduleRetry();
                return;
            }

            Log.Info($"已发送 C_INTERACT_ARMORY armory=#{armory.ID} type={interactType}（尝试 {_attempts}/{max}）");
            // 不立刻 done：下一拍检查是否已持刀；短暂间隔后再试
            _nextTryRealtime = Time.realtimeSinceStartup + 0.8f;
        }

        private void ScheduleRetry(float? overrideInterval = null)
        {
            float interval = overrideInterval
                ?? (RetryInterval != null ? Mathf.Max(0.2f, RetryInterval.Value) : 1f);
            _nextTryRealtime = Time.realtimeSinceStartup + interval;
            _attempts++;
        }

        private static bool TryGetState(out EGameState state)
        {
            state = EGameState.NoneState;
            try
            {
                if (Managers.Host != null && Managers.Host.IsHost)
                {
                    var room = GameRoom.Instance;
                    if (room == null) return false;
                    state = room.State;
                    return true;
                }
                if (Managers.Game == null) return false;
                state = Managers.Game.State;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ResetTracking()
        {
            _lastState = EGameState.NoneState;
            _doneThisRound = false;
            _phaseEnterRealtime = -1f;
            _nextTryRealtime = 0f;
            _attempts = 0;
        }
    }
}
