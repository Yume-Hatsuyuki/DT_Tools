using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Game
{
    public static class PlayerName
    {
        public const int MaxLength = 64;

        public static bool TrySanitize(string raw, out string name, out string error)
        {
            name = null;
            error = null;
            string trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "新昵称不能为空。";
                return false;
            }
            trimmed = Util.NeutralizeRichText(trimmed).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "新昵称清洗后为空（不能只含富文本标记）。";
                return false;
            }
            name = trimmed.Length > MaxLength ? trimmed.Substring(0, MaxLength) : trimmed;
            return true;
        }

        public static void ApplyServerName(Server.Game.Player target, string newName)
        {
            AccessTools.Property(typeof(Server.Game.Player), nameof(Server.Game.Player.Name))
                ?.SetValue(target, newName);
        }

        public static bool TryRefreshLocalCache(int playerId, string newName)
        {
            try
            {
                var cache = Traverse.Create(Managers.Player)
                    .Field<Dictionary<int, Player>>("_cache")
                    .Value;
                if (cache == null || !cache.TryGetValue(playerId, out var clientPlayer) || clientPlayer == null)
                    return false;
                Traverse.Create(clientPlayer).Property("Name").SetValue(newName);
                clientPlayer.RefreshNameTag();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("PlayerName", $"本机显示名同步失败: {ex.Message}");
                return false;
            }
        }

        public static void SetLocalName(string name)
        {
            Managers.Player.MyPlayerName = name;
            try
            {
                string key = $"LobbyNickname_{Managers.Network.LocalAccountKey}";
                PlayerPrefs.SetString(key, name);
            }
            catch (Exception ex)
            {
                Log.Warn("PlayerName", $"昵称本地存档失败: {ex.Message}");
            }

            try
            {
                var scene = UnityEngine.Object.FindFirstObjectByType<UI_LobbyScene>();
                if (scene == null)
                    return;
                var field = Traverse.Create(scene)
                    .Method("GetInputField", new object[] { 1 })
                    .GetValue<TMPro.TMP_InputField>();
                if (field != null)
                    field.text = name;
            }
            catch
            {
            }
        }
    }
}
