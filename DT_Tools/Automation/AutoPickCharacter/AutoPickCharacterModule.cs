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
        public static ConfigEntry<bool> SyncLobby;

        public bool ModuleEnabled => Enabled != null && Enabled.Value;

        private bool _bound;
        private EGameState _lastState = EGameState.NoneState;
        private bool _sentPickThisPhase;
        private float _phaseEnterRealtime = -1f;
        private bool _waitingLogged;
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
                0.6f,
                "进入选角阶段后延迟多少秒再发包。");
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
                    _sentPickThisPhase = false;
                    _phaseEnterRealtime = Time.realtimeSinceStartup;
                    _waitingLogged = false;
                    Log.Info("已进入选角阶段");
                }
                else if (_lastState == EGameState.PickCharacter)
                {
                    Log.Info($"离开选角阶段 → {state}");
                    _sentPickThisPhase = false;
                    _phaseEnterRealtime = -1f;
                    _waitingLogged = false;
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
            if (_sentPickThisPhase)
                return;

            float delay = DelaySeconds != null ? Mathf.Max(0f, DelaySeconds.Value) : 0.6f;
            if (_phaseEnterRealtime < 0f)
                _phaseEnterRealtime = Time.realtimeSinceStartup;

            if (Time.realtimeSinceStartup - _phaseEnterRealtime < delay)
            {
                if (!_waitingLogged)
                {
                    Log.Info($"等待 {delay:0.##}s 后发包");
                    _waitingLogged = true;
                }
                return;
            }

            int id = ResolvePickId();
            if (!CharacterCatalog.IsKnown(id) && id != CharacterCatalog.RandomId)
                Log.Warn($"CharacterId={id} 不在当前角色表中，仍将发送");

            var packet = new C_PICK_CHARACTER { CharacterId = id };
            if (!ClientPacket.TrySend(packet, out string err))
            {
                Log.Warn($"选角发送失败: {err}");
                return;
            }

            _sentPickThisPhase = true;
            Log.Info($"已发送 C_PICK_CHARACTER CharacterId={id}（{CharacterCatalog.GetDisplayName(id)}）");
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
            return CharacterId != null ? CharacterId.Value : 102;
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
            _sentPickThisPhase = false;
            _phaseEnterRealtime = -1f;
            _waitingLogged = false;
        }
    }
}
