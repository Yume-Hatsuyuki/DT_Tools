using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Game
{
    /// <summary>
    /// 玩家昵称行为助手（/nick、/myname 共用）：清洗、服务端改名、本机显示同步。
    /// 服务端无改名广播包（S_MODIFY_PLAYER 事件不含名字）——Server.Game.Player.Name
    /// 为 private set 自动属性（0.1.15b Server.Game/Player.cs:68，仅构造时赋值），
    /// 经属性 setter 反射写入；新名字对后续加入者（进房握手逐个下发 S_ADD_PLAYER，
    /// 0.1.15b GameRoom.cs:1183-1190）生效，已在线客户端不做变更。
    /// </summary>
    public static class PlayerName
    {
        /// <summary>服务端记录/命令链路统一上限（对齐 CustomRoomName 的 64 截断）。</summary>
        public const int MaxLength = 64;

        /// <summary>
        /// 昵称清洗的唯一实现：trim → 去富文本 → trim → 截断
        /// （0.1.15b Util.cs:172 NeutralizeRichText，与服务端进房清洗同一道）。
        /// </summary>
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

        /// <summary>服务端改名（反射写 private set；调用方负责已在线客户端不做变更的文案）。</summary>
        public static void ApplyServerName(Server.Game.Player target, string newName)
        {
            AccessTools.Property(typeof(Server.Game.Player), nameof(Server.Game.Player.Name))
                ?.SetValue(target, newName);
        }

        /// <summary>
        /// 房主本机客户端缓存同步（尽力而为）：客户端 Player.Name 为 protected set
        /// （0.1.15b Player.cs:276），经 Traverse 写入后调 RefreshNameTag（public，:462）。
        /// </summary>
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

        /// <summary>
        /// 本地昵称（主页输入值）：写 PlayerManager.MyPlayerName（public set，:46）+
        /// PlayerPrefs（键 = "LobbyNickname_"+LocalAccountKey，0.1.15b UI_LobbyScene.cs:501，
        /// NetworkManager.cs:142）+ 大厅输入框同步（尽力而为，赋值触发原版校验链）。
        /// </summary>
        public static void SetLocalName(string name)
        {
            Managers.Player.MyPlayerName = name;
            try
            {
                string key = "LobbyNickname_" + Managers.Network.LocalAccountKey;
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
                    .Method("GetInputField", new object[] { 1 })   // 昵称输入框：UI_Base.cs:98，使用处 UI_LobbyScene.cs:502
                    .GetValue<TMPro.TMP_InputField>();
                if (field != null)
                    field.text = name;
            }
            catch
            {
                // 不在大厅场景时静默跳过
            }
        }
    }
}
