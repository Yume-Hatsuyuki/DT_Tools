using System;
using System.Collections.Generic;
using DT_Tools.Core;
using Steamworks;

namespace DT_Tools.Game
{
    public static class RoomLobbyData
    {
        private static readonly HashSet<string> Wired = new HashSet<string>();

        public static void WireChanged<TFeature>(string fieldName, Action<string> apply)
        {
            string key = Engine.SectionOf<TFeature>() + $"/{fieldName}";
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
