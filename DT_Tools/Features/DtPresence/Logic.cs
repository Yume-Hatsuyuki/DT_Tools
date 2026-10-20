using System;
using System.Collections.Generic;
using DT_Tools.Core;
using Steamworks;
using TMPro;
using UnityEngine;

namespace DT_Tools.Features.DtPresence
{
    /// <summary>
    /// 在场暴露业务：写自己的 Lobby Member Data、扫描他人、驱动名字颜色。
    /// </summary>
    internal static class DtPresenceLogic
    {
        /// <summary>
        /// Steam Lobby Member Data 键（与游戏 <c>build</c> 游戏版本键区分）。
        /// </summary>
        public const string MemberDataKey = "dt_tools";

        /// <summary>
        /// 原版黑方名字色（0.1.18c UI_NameTag.SetNameColor）。
        /// </summary>
        public static readonly Color BlackNameColor = new Color(1f, 0.18f, 0.439f);

        private static HostComponent _host;
        private static string _lastPublished = null;
        private static readonly Dictionary<int, MarkDriver> _drivers = new Dictionary<int, MarkDriver>();

        /// <summary>
        /// 确保常驻宿主存在（OnPatched / 热开共用）。
        /// </summary>
        public static void EnsureHost()
        {
            if (_host != null)
                return;
            var go = new GameObject("DT_DtPresenceHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _host = go.AddComponent<HostComponent>();
        }

        public static void OnFeatureEnabled()
        {
            EnsureHost();
            _lastPublished = null;
            TryPublish("OnEnabled");
        }

        public static void OnFeatureDisabled()
        {
            ClearOwnMemberData("OnDisabled");
            ClearAllMarks();
            _lastPublished = null;
        }

        /// <summary>
        /// 当前应写入的 value：功能关 → 清空；开且公布版本 → 版本号；开但不公布版本 → "1"。
        /// </summary>
        public static string DesiredValue()
        {
            if (!Engine.Enabled<DtPresenceFeature>())
                return "";
            if (DtPresenceFeature.PublishVersion)
                return MyPluginInfo.PLUGIN_VERSION ?? "1";
            return "1";
        }

        /// <summary>
        /// 写入或清除自己的 Member Data（仅本机 Steam 身份）。
        /// </summary>
        public static void TryPublish(string reason)
        {
            try
            {
                var lobby = Managers.Network?.Lobby;
                if (lobby == null || !lobby.InLobby || lobby.LobbyId == CSteamID.Nil)
                    return;

                string value = DesiredValue();
                if (value == _lastPublished)
                    return;

                SteamMatchmaking.SetLobbyMemberData(lobby.LobbyId, MemberDataKey, value);
                _lastPublished = value;
                if (string.IsNullOrEmpty(value))
                    Log.Info<DtPresenceFeature>($"已清除本机 {MemberDataKey}（{reason}）");
                else
                    Log.Info<DtPresenceFeature>($"已公布本机 {MemberDataKey}={value}（{reason}）");
            }
            catch (Exception ex)
            {
                Log.Debug<DtPresenceFeature>($"TryPublish 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 功能关闭时尽量清空自己的键（仍在大厅时）。
        /// </summary>
        public static void ClearOwnMemberData(string reason)
        {
            try
            {
                var lobby = Managers.Network?.Lobby;
                if (lobby == null || !lobby.InLobby || lobby.LobbyId == CSteamID.Nil)
                {
                    _lastPublished = null;
                    return;
                }
                SteamMatchmaking.SetLobbyMemberData(lobby.LobbyId, MemberDataKey, "");
                _lastPublished = "";
                Log.Info<DtPresenceFeature>($"已清除本机 {MemberDataKey}（{reason}）");
            }
            catch (Exception ex)
            {
                Log.Debug<DtPresenceFeature>($"ClearOwnMemberData 失败: {ex.Message}");
                _lastPublished = null;
            }
        }

        /// <summary>
        /// 读取某 Steam 用户在当前大厅的 dt_tools 值。
        /// </summary>
        public static string ReadMemberDtTools(CSteamID steamId)
        {
            try
            {
                var lobby = Managers.Network?.Lobby;
                if (lobby == null || !lobby.InLobby || lobby.LobbyId == CSteamID.Nil
                    || steamId == CSteamID.Nil)
                    return "";
                return SteamMatchmaking.GetLobbyMemberData(lobby.LobbyId, steamId, MemberDataKey) ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 解析配置中的标记色；失败时回退青蓝。
        /// </summary>
        public static Color ResolveMarkColor()
        {
            string raw = (DtPresenceFeature.MarkColor ?? "").Trim();
            if (raw.Length > 0 && !raw.StartsWith("#", StringComparison.Ordinal))
                raw = "#" + raw;
            if (ColorUtility.TryParseHtmlString(raw, out Color c))
                return c;
            return new Color(0.31f, 0.76f, 0.97f); // #4FC3F7
        }

        /// <summary>
        /// 每帧/节流：维护公布 + 同模标记。
        /// </summary>
        public static void Tick()
        {
            if (!Engine.Enabled<DtPresenceFeature>())
            {
                if (_drivers.Count > 0)
                    ClearAllMarks();
                return;
            }

            var lobby = Managers.Network?.Lobby;
            bool inLobby = lobby != null && lobby.InLobby && lobby.LobbyId != CSteamID.Nil;

            if (inLobby)
                TryPublish("Tick");
            else
                _lastPublished = null;

            if (!DtPresenceFeature.MarkOthers)
            {
                if (_drivers.Count > 0)
                    ClearAllMarks();
                return;
            }

            if (Managers.Player == null)
                return;

            var players = Managers.Player.GetAllPlayers();
            if (players == null)
                return;

            int myId = Managers.Player.MyPlayerID;
            var live = new HashSet<int>();

            foreach (var player in players)
            {
                if (player?.PublicInfo == null || player.NameTag == null)
                    continue;

                int pid = player.PublicInfo.PlayerId;
                if (pid == myId)
                    continue; // 只标记其他人

                ulong steam = Managers.Player.GetRosterSteamId(pid);
                if (steam == 0)
                    continue;

                string dt = inLobby
                    ? ReadMemberDtTools(new CSteamID(steam))
                    : "";
                if (string.IsNullOrEmpty(dt))
                {
                    RemoveMark(pid);
                    continue;
                }

                live.Add(pid);
                EnsureDriver(pid, player);
            }

            // 清理已离开或不在场的驱动
            if (_drivers.Count > live.Count)
            {
                var stale = new List<int>();
                foreach (var kv in _drivers)
                {
                    if (!live.Contains(kv.Key))
                        stale.Add(kv.Key);
                }
                foreach (int pid in stale)
                    RemoveMark(pid);
            }
        }

        private static void EnsureDriver(int pid, Player player)
        {
            if (_drivers.TryGetValue(pid, out var existing) && existing != null)
            {
                existing.Bind(player);
                return;
            }

            var go = player.NameTag.gameObject;
            var driver = go.GetComponent<MarkDriver>();
            if (driver == null)
                driver = go.AddComponent<MarkDriver>();
            driver.Bind(player);
            _drivers[pid] = driver;
        }

        private static void RemoveMark(int pid)
        {
            if (!_drivers.TryGetValue(pid, out var driver))
                return;
            _drivers.Remove(pid);
            if (driver != null)
            {
                driver.RestoreAndDestroy();
            }
        }

        private static void ClearAllMarks()
        {
            var ids = new List<int>(_drivers.Keys);
            foreach (int pid in ids)
                RemoveMark(pid);
            _drivers.Clear();
        }

        /// <summary>
        /// 常驻宿主：节流 Tick。
        /// </summary>
        private sealed class HostComponent : MonoBehaviour
        {
            private float _nextPublishCheck;
            private float _nextMarkScan;

            private void Update()
            {
                float now = Time.unscaledTime;

                // 公布：约 1.5s 一次（进房/热改也能较快跟上）
                if (now >= _nextPublishCheck)
                {
                    _nextPublishCheck = now + 1.5f;
                    if (Engine.Enabled<DtPresenceFeature>())
                        TryPublish("Host");
                }

                // 标记扫描稍密：0.5s
                if (now >= _nextMarkScan)
                {
                    _nextMarkScan = now + 0.5f;
                    Tick();
                }
            }
        }

        /// <summary>
        /// 挂在目标 NameTag 上：每帧 LateUpdate 覆盖颜色；黑方时呼吸。
        /// </summary>
        internal sealed class MarkDriver : MonoBehaviour
        {
            private Player _player;
            private TMP_Text _text;
            private bool _restoring;

            public void Bind(Player player)
            {
                _player = player;
                if (_text == null && player?.NameTag != null)
                    _text = player.NameTag.GetComponentInChildren<TMP_Text>(true);
            }

            private void LateUpdate()
            {
                if (_restoring)
                    return;
                if (!Engine.Enabled<DtPresenceFeature>() || !DtPresenceFeature.MarkOthers)
                {
                    RestoreAndDestroy();
                    return;
                }
                if (_player == null || _player.NameTag == null)
                {
                    Destroy(this);
                    return;
                }
                if (_text == null)
                    _text = _player.NameTag.GetComponentInChildren<TMP_Text>(true);
                if (_text == null)
                    return;

                Color mark = ResolveMarkColor();
                bool isBlack = false;
                try
                {
                    int pid = _player.PublicInfo != null ? _player.PublicInfo.PlayerId : 0;
                    isBlack = pid > 0 && Managers.Player != null
                              && Managers.Player.KnownBlackIds != null
                              && Managers.Player.KnownBlackIds.Contains(pid);
                }
                catch
                {
                    // 关房瞬间可能空引用
                }

                if (isBlack)
                {
                    float period = Mathf.Max(0.4f, DtPresenceFeature.PulsePeriod);
                    // 0→1→0 呼吸
                    float t = Mathf.PingPong(Time.unscaledTime / (period * 0.5f), 1f);
                    _text.color = Color.Lerp(BlackNameColor, mark, t);
                }
                else
                {
                    _text.color = mark;
                }
            }

            /// <summary>
            /// 还原原版颜色后销毁自身。
            /// </summary>
            public void RestoreAndDestroy()
            {
                if (_restoring)
                    return;
                _restoring = true;
                try
                {
                    if (_player?.NameTag != null)
                    {
                        bool isBlack = false;
                        try
                        {
                            int pid = _player.PublicInfo != null ? _player.PublicInfo.PlayerId : 0;
                            isBlack = pid > 0 && Managers.Player?.KnownBlackIds != null
                                      && Managers.Player.KnownBlackIds.Contains(pid);
                        }
                        catch { /* ignore */ }
                        _player.NameTag.SetNameColor(isBlack);
                    }
                }
                catch
                {
                    // NameTag 可能已销毁
                }
                Destroy(this);
            }
        }
    }
}
