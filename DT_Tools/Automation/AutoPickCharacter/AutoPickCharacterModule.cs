using System;
using BepInEx.Configuration;
using Protocol;
using Server.Game;
using DT_Tools.Automation.Shared;
using DT_Tools.Core;
using UnityEngine;

namespace DT_Tools.Automation.AutoPickCharacter
{
    /// <summary>
    /// 大厅同步外观 + 选角阶段自动 C_PICK_CHARACTER。
    /// 角色列表运行时从 CharacterDic 读取（含梅德琳）。
    ///
    /// 可靠性：服务端在 StartPick 的 SyncAllPlayer 回调前 _pickReady=false，
    /// 过早的 C_PICK_CHARACTER 会被静默忽略。因此在延迟后按间隔重试，
    /// 直到离开选角阶段或达到最大次数（服务端对已选玩家幂等）。
    /// </summary>
    [AutomationModule(
        id: "pick-character",
        section: "Auto.PickCharacter",
        displayName: "自动选择角色",
        description: "大厅使用所选角色模型；进入选角阶段后自动发送 C_PICK_CHARACTER（指定或随机）。",
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal sealed class AutoPickCharacterModule : IAutomationModule
    {
        public string Id => "pick-character";
        public string Section => "Auto.PickCharacter";
        public string DisplayName => "自动选择角色";
        public string Description =>
            "大厅使用所选角色模型；进入选角阶段后自动发送 C_PICK_CHARACTER（指定或随机）。";
        public FeatureSide Side => FeatureSide.Client;
        public string Author => "梦初雪";

        public AutomationLogBuffer Log { get; } = new AutomationLogBuffer();

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<AutoPickMode> Mode;
        public static ConfigEntry<int> CharacterId;
        public static ConfigEntry<float> DelaySeconds;
        public static ConfigEntry<float> RetryInterval;
        public static ConfigEntry<int> MaxAttempts;
        public static ConfigEntry<bool> SyncLobby;
        public static ConfigEntry<bool> FallbackToRandom;

        public bool ModuleEnabled => Enabled != null && Enabled.Value;

        private bool _bound;
        private EGameState _lastState = EGameState.NoneState;
        private float _phaseEnterRealtime = -1f;
        private float _nextTryRealtime;
        private int _attempts;
        private bool _waitingLogged;
        private bool _gaveUpLogged;
        private int _lastLobbySyncedId = int.MinValue;
        private float _nextLobbyTryRealtime;

        public void BindConfig(ConfigFile config)
        {
            if (_bound) return;
            Enabled = config.Bind(
                Section,
                "Enabled",
                false,
                "是否启用：大厅同步外观 + 选角阶段自动选角。");
            Mode = config.Bind(
                Section,
                "Mode",
                AutoPickMode.Fixed,
                "Fixed=使用下方角色；Random=随机（仅选角包 CharacterId=-2）。");
            CharacterId = config.Bind(
                Section,
                "CharacterId",
                102,
                "指定角色 DataId（含 101 梅德琳）。");
            OptionProviders.Bind(Section, "CharacterId", CharacterCatalog.Options);
            DelaySeconds = config.Bind(
                Section,
                "DelaySeconds",
                1.2f,
                "进入选角阶段后延迟多少秒再开始发包（需等服务端 _pickReady）。");
            RetryInterval = config.Bind(
                Section,
                "RetryInterval",
                0.8f,
                "发包后仍未确认时的重试间隔（秒）。过早包会被服务端忽略。");
            MaxAttempts = config.Bind(
                Section,
                "MaxAttempts",
                20,
                "本阶段最多发送次数。达到后停止（避免无限刷包）。");
            FallbackToRandom = config.Bind(
                Section,
                "FallbackToRandom",
                true,
                "Fixed 模式下，后半次尝试改为随机（-2），避免目标角色已被他人占用。");
            SyncLobby = config.Bind(
                Section,
                "SyncLobby",
                true,
                "在大厅时发送 ChangeCharacter，使自己的模型与所选角色一致。");
            _bound = true;
            Log.Info("配置已绑定");
        }

        public void Tick(bool hostEnabled)
        {
            if (!hostEnabled || !ModuleEnabled)
            {
                ResetPhaseTracking();
                _lastLobbySyncedId = int.MinValue;
                return;
            }

            if (!TryGetState(out EGameState state))
                return;

            if (state != _lastState)
            {
                if (state == EGameState.PickCharacter)
                {
                    _phaseEnterRealtime = Time.realtimeSinceStartup;
                    _nextTryRealtime = 0f;
                    _attempts = 0;
                    _waitingLogged = false;
                    _gaveUpLogged = false;
                    Log.Info("已进入选角阶段");
                }
                else if (_lastState == EGameState.PickCharacter)
                {
                    Log.Info($"离开选角阶段 → {state}（本阶段尝试 {_attempts} 次）");
                    ResetPhaseTracking();
                }

                if (state == EGameState.Lobby)
                {
                    _lastLobbySyncedId = int.MinValue;
                    _nextLobbyTryRealtime = 0f;
                    Log.Info("已进入大厅");
                }

                _lastState = state;
            }

            if (state == EGameState.Lobby && (SyncLobby == null || SyncLobby.Value))
                TrySyncLobbyCharacter();

            if (state == EGameState.PickCharacter)
                TryAutoPick();
        }

        private void TryAutoPick()
        {
            float delay = DelaySeconds != null ? Mathf.Max(0f, DelaySeconds.Value) : 1.2f;
            if (_phaseEnterRealtime < 0f)
                _phaseEnterRealtime = Time.realtimeSinceStartup;

            if (Time.realtimeSinceStartup - _phaseEnterRealtime < delay)
            {
                if (!_waitingLogged)
                {
                    Log.Info($"等待 {delay:0.##}s 后开始发包（等服务端选角就绪）");
                    _waitingLogged = true;
                }
                return;
            }

            int maxAttempts = MaxAttempts != null ? Mathf.Max(1, MaxAttempts.Value) : 20;
            if (_attempts >= maxAttempts)
            {
                if (!_gaveUpLogged)
                {
                    Log.Warn($"已达最大尝试次数 {maxAttempts}，停止本阶段发包");
                    _gaveUpLogged = true;
                }
                return;
            }

            float interval = RetryInterval != null ? Mathf.Max(0.1f, RetryInterval.Value) : 0.8f;
            if (_attempts > 0 && Time.realtimeSinceStartup < _nextTryRealtime)
                return;

            int id = ResolvePickId();
            if (!CharacterCatalog.IsKnown(id) && id != CharacterCatalog.RandomId)
                Log.Warn($"CharacterId={id} 不在当前角色表中，仍将发送");

            var packet = new C_PICK_CHARACTER { CharacterId = id };
            if (!ClientPacket.TrySend(packet, out string err))
            {
                Log.Warn($"选角发送失败: {err}");
                _nextTryRealtime = Time.realtimeSinceStartup + interval;
                return;
            }

            _attempts++;
            _nextTryRealtime = Time.realtimeSinceStartup + interval;
            Log.Info(
                $"已发送 C_PICK_CHARACTER CharacterId={id}（{CharacterCatalog.GetDisplayName(id)}）" +
                $" 第{_attempts}/{maxAttempts}次");
        }

        private void TrySyncLobbyCharacter()
        {
            if (Mode != null && Mode.Value == AutoPickMode.Random)
                return;

            int id = CharacterId != null ? CharacterId.Value : 102;
            if (id == CharacterCatalog.RandomId)
                return;

            if (Time.realtimeSinceStartup < _nextLobbyTryRealtime)
                return;

            try
            {
                if (Managers.Player?.MyPlayer == null)
                {
                    _nextLobbyTryRealtime = Time.realtimeSinceStartup + 0.5f;
                    return;
                }

                int current = Managers.Player.MyPlayer.PublicInfo.CharacterId;
                if (current == id && _lastLobbySyncedId == id)
                    return;

                if (current == id)
                {
                    _lastLobbySyncedId = id;
                    return;
                }

                var packet = new C_MODIFY_PLAYER
                {
                    Type = EModifyPlayerEvent.ChangeCharacter,
                    Value = id
                };
                if (!ClientPacket.TrySend(packet, out string err))
                {
                    Log.Warn($"大厅换角失败: {err}");
                    _nextLobbyTryRealtime = Time.realtimeSinceStartup + 1.5f;
                    return;
                }

                _lastLobbySyncedId = id;
                _nextLobbyTryRealtime = Time.realtimeSinceStartup + 1.0f;
                Log.Info($"大厅已请求换角 → {id}（{CharacterCatalog.GetDisplayName(id)}）");
            }
            catch (Exception ex)
            {
                Log.Warn($"大厅换角异常: {ex.Message}");
                _nextLobbyTryRealtime = Time.realtimeSinceStartup + 2f;
            }
        }

        private int ResolvePickId()
        {
            if (Mode != null && Mode.Value == AutoPickMode.Random)
                return CharacterCatalog.RandomId;

            int fixedId = CharacterId != null ? CharacterId.Value : 102;

            // 后半次尝试回退随机，避免目标角色已被占用时一直空选
            bool fallback = FallbackToRandom == null || FallbackToRandom.Value;
            if (fallback && _attempts > 0)
            {
                int maxAttempts = MaxAttempts != null ? Mathf.Max(1, MaxAttempts.Value) : 20;
                if (_attempts >= Math.Max(1, maxAttempts / 2))
                {
                    if (_attempts == Math.Max(1, maxAttempts / 2))
                        Log.Info($"Fixed 目标可能被占用，后续尝试改用随机 ({CharacterCatalog.RandomId})");
                    return CharacterCatalog.RandomId;
                }
            }

            return fixedId;
        }

        private static bool TryGetState(out EGameState state)
        {
            state = EGameState.NoneState;
            try
            {
                bool isHost = Managers.Host != null && Managers.Host.IsHost;
                if (isHost)
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

        private void ResetPhaseTracking()
        {
            _lastState = EGameState.NoneState;
            _phaseEnterRealtime = -1f;
            _nextTryRealtime = 0f;
            _attempts = 0;
            _waitingLogged = false;
            _gaveUpLogged = false;
        }
    }
}
