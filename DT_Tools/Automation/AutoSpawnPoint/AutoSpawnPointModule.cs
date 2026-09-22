using System;
using BepInEx.Configuration;
using Protocol;
using Server.Game;
using DT_Tools.Automation.Shared;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    /// <summary>
    /// 进入 Survive 后传送到指定出生点或自定义坐标（与 /beacon 同源）。
    /// </summary>
    [AutomationModule(
        id: "spawn-point",
        section: "Auto.SpawnPoint",
        displayName: "开局指定出生点",
        description: "进入生存阶段后传送到 StartPosList 出生点或自定义坐标。",
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal sealed class AutoSpawnPointModule : IAutomationModule
    {
        public string Id => "spawn-point";
        public string Section => "Auto.SpawnPoint";
        public string DisplayName => "开局指定出生点";
        public string Description =>
            "进入生存阶段后传送到 StartPosList 出生点（下拉）或自定义坐标。";
        public FeatureSide Side => FeatureSide.Client;
        public string Author => "梦初雪";

        public AutomationLogBuffer Log { get; } = new AutomationLogBuffer();

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<AutoSpawnMode> Mode;
        public static ConfigEntry<int> SpawnIndex;
        public static ConfigEntry<float> PosX;
        public static ConfigEntry<float> PosY;
        public static ConfigEntry<float> DelaySeconds;

        public bool ModuleEnabled => Enabled != null && Enabled.Value;

        private bool _bound;
        private EGameState _lastState = EGameState.NoneState;
        private bool _doneThisRound;
        private float _phaseEnterRealtime = -1f;
        private bool _waitingLogged;

        public void BindConfig(ConfigFile config)
        {
            if (_bound) return;
            Enabled = config.Bind(
                Section,
                "Enabled",
                false,
                "是否启用：进入 Survive 后自动传送。");
            Mode = config.Bind(
                Section,
                "Mode",
                AutoSpawnMode.Index,
                "Index=下拉选择出生点序号；Custom=使用下方 PosX / PosY 自定义坐标。");
            SpawnIndex = config.Bind(
                Section,
                "SpawnIndex",
                1,
                "出生点序号（1-based）。Mode=Index 时生效；界面以下拉展示坐标与房间。");
            OptionProviders.Bind(Section, "SpawnIndex", SpawnCatalog.Options);
            PosX = config.Bind(
                Section,
                "PosX",
                0f,
                "自定义坐标 X。Mode=Custom 时生效。");
            PosY = config.Bind(
                Section,
                "PosY",
                0f,
                "自定义坐标 Y。Mode=Custom 时生效。");
            DelaySeconds = config.Bind(
                Section,
                "DelaySeconds",
                15f,
                "进入 Survive 后延迟多少秒再传送。");
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
                    _waitingLogged = false;
                    Log.Info("已进入 Survive，准备传送");
                }
                else if (_lastState == EGameState.Survive)
                {
                    Log.Info($"离开 Survive → {state}");
                    _doneThisRound = true;
                }
                _lastState = state;
            }

            if (state == EGameState.Survive && !_doneThisRound)
                TryTeleport();
        }

        private void TryTeleport()
        {
            float delay = DelaySeconds != null ? Mathf.Max(0f, DelaySeconds.Value) : 0.5f;
            if (_phaseEnterRealtime < 0f)
                _phaseEnterRealtime = Time.realtimeSinceStartup;

            if (Time.realtimeSinceStartup - _phaseEnterRealtime < delay)
            {
                if (!_waitingLogged)
                {
                    Log.Info($"等待 {delay:0.##}s 后传送");
                    _waitingLogged = true;
                }
                return;
            }

            var my = Managers.Player?.MyPlayer;
            if (my == null || my.PrivateInfo == null)
                return;

            if (my.State == EPlayerState.Hide || my.State == EPlayerState.Sit)
            {
                Log.Warn($"状态 {my.State} 无法传送，本局跳过");
                _doneThisRound = true;
                return;
            }

            if (!TryResolveTarget(out PosInfo target, out string label))
            {
                _doneThisRound = true;
                return;
            }

            if (!LocalTeleport.TryTeleport(my, target, out string err))
            {
                Log.Warn($"传送失败: {err}");
                _phaseEnterRealtime = Time.realtimeSinceStartup;
                return;
            }

            _doneThisRound = true;
            Log.Info($"已传送到 {label} → ({target.X:F0}, {target.Y:F0})");
        }

        private bool TryResolveTarget(out PosInfo target, out string label)
        {
            target = null;
            label = null;
            var mode = Mode != null ? Mode.Value : AutoSpawnMode.Index;

            if (mode == AutoSpawnMode.Custom)
            {
                float x = PosX != null ? PosX.Value : 0f;
                float y = PosY != null ? PosY.Value : 0f;
                target = new PosInfo { X = x, Y = y };
                label = "自定义坐标";
                return true;
            }

            var list = Managers.Data?.MapData?.StartPosList;
            if (list == null || list.Count == 0)
            {
                Log.Warn("StartPosList 为空，无法按序号传送");
                return false;
            }

            int idx = SpawnIndex != null ? SpawnIndex.Value : 1;
            if (idx < 1 || idx > list.Count)
            {
                Log.Warn($"SpawnIndex={idx} 超出范围 1..{list.Count}");
                return false;
            }

            var src = list[idx - 1];
            target = new PosInfo { X = src.X, Y = src.Y };
            label = $"出生点 #{idx}";
            return true;
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
            _waitingLogged = false;
        }
    }
}
