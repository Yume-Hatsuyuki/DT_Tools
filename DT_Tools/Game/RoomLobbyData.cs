using System;
using System.Collections.Generic;
using DT_Tools.Core;
using Steamworks;

namespace DT_Tools.Game
{
    /// <summary>
    /// Steam 大厅自定义数据（房间码/房间名）骨架：CustomRoomCode / CustomRoomName
    /// 两个功能域同构的「配置热改订阅 + 房主写 Lobby」收口于此（AGENTS.md §4：≥2 处才上浮）。
    /// 清洗规则各域不同（码=A–Z/0–9 共 7 位、名=去富文本后 64 字符），留在各自 Logic。
    /// 统一门闩语义：功能未开启时静默返回 false（原先两域一个静默、一个打日志，不一致）。
    /// </summary>
    public static class RoomLobbyData
    {
        /// <summary>已挂接的「段/字段」订阅键，防止重复订阅（TryGetEntry 失败时不入集，可重试）。</summary>
        private static readonly HashSet<string> Wired = new HashSet<string>();

        /// <summary>
        /// 订阅字符串配置项变更（功能字段是裸 string，底层 BepInEx ConfigEntry 经
        /// Engine.Config 查回）。变更且功能开启时以「字段名.Changed」原因回调 apply。幂等。
        /// </summary>
        public static void WireChanged<TFeature>(string fieldName, Action<string> apply)
        {
            string key = Engine.SectionOf<TFeature>() + "/" + fieldName;
            if (Wired.Contains(key))
                return;
            if (!Engine.Config.TryGetEntry<string>(
                    Engine.SectionOf<TFeature>(), fieldName, out var entry))
                return;

            entry.SettingChanged += (_, __) =>
            {
                if (Engine.Enabled<TFeature>())
                    apply($"{fieldName}.Changed");
            };
            Wired.Add(key);
        }

        /// <summary>
        /// 把已清洗的值写入当前 Steam Lobby（需房主）。返回写入是否成功；
        /// value 为空由调用方先判（空值语义各域不同：码=放行原版随机码、名=不写）。
        /// <paramref name="onSet"/> 在 SetLobbyData 调用后必调（无论成败），用于同步本地显示等跟随动作。
        /// </summary>
        public static bool TryApplyLobbyData<TFeature>(
            string dataKey, string value, string label, string reason, Action onSet = null)
        {
            if (!Engine.Enabled<TFeature>())
                return false;

            var lobby = Managers.Network?.Lobby;
            if (lobby == null || lobby.LobbyId == CSteamID.Nil)
            {
                Log.Info<TFeature>($"尚无 Lobby（{reason}），待建房/进房后再写");
                return false;
            }
            if (!lobby.IsHost)
            {
                Log.Warn<TFeature>($"非房主，无法写入{label}");
                return false;
            }

            bool ok = SteamMatchmaking.SetLobbyData(lobby.LobbyId, dataKey, value);
            onSet?.Invoke();
            Log.Info<TFeature>(ok ? $"{label}已写入: {value}（{reason}）" : $"SetLobbyData 失败: {value}（{reason}）");
            return ok;
        }
    }
}
