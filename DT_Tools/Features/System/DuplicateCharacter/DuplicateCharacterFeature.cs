using System.Collections.Generic;
using System.Linq;
using Data;
using HarmonyLib;
using Protocol;
using Server.Game;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    [HarmonyPatch]
    [PatchFeature(
        section: "PickCharacter",
        description: "重复角色：多名玩家可选择同一角色（房主启用即可）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static partial class DuplicateCharacterFeature
    {
        // BuildPickCandidates 只读 taken（Contains），共享一个空集即可，避免每次分配分配新对象
        private static readonly HashSet<int> EmptyTaken = new HashSet<int>();

        // 本轮揭晓是否已完成（进入值 0 时重置，>=40 揭晓后置位，防止 40~44 五个 tick 重复）
        private static bool _revealed;

        // ── GameRoom.PickCharacter：去掉角色唯一性闸门，其余与原版一致 ─────────
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.PickCharacter))]
        [HarmonyPrefix]
        private static bool PrefixPickCharacter(GameRoom __instance, GamePlayer player, int characterId)
        {
            if (!FeatureGate.Enabled(typeof(DuplicateCharacterFeature)))
                return true;

            if (__instance.State != EGameState.PickCharacter)
                return false;

            var t = Traverse.Create(__instance);
            if (!t.Field("_pickReady").GetValue<bool>())
            {
                Debug.Log($"[Pick] PickCharacter ignored - phase not ready yet. playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            if (TimeManager.Instance.StopWatch >= 40)
                return false;

            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();
            var randomPickPlayers = t.Field("_randomPickPlayers").GetValue<HashSet<int>>();

            int playerId = player.PublicInfo.PlayerId;

            if (characterId == -2)
            {
                if (!pickPlayers.Contains(playerId) && randomPickPlayers.Add(playerId))
                {
                    Debug.Log($"[Pick] Random pick reserved - playerId:{playerId}");
                    // 只回选择者本人：其他客户端收不到就不会置灰格子（原版广播会触发
                    // 对端的 Selected 锁定，挡住后续重复选择请求）
                    player.Session?.Send(new S_PICK_CHARACTER
                    {
                        CharacterId = -2,
                        PlayerId = playerId
                    });
                    InvokeCheckPickAllDone(__instance);
                }
                return false;
            }

            // 与原版唯一差异：不再要求 !_pickCharacters.Contains(characterId)
            if (Managers.Data.CharacterDic.Values
                    .FirstOrDefault((CharacterData x) => x.DataId == characterId) == null)
                return false;

            if (pickPlayers.Contains(playerId) || randomPickPlayers.Contains(playerId))
                return false;

            pickPlayers.Add(playerId);
            // 必须继续追加：与 _pickPlayers 保持下标一一对应，CancelPickCharacter 按下标联动删除
            pickCharacters.Add(characterId);
            player.CharacterId = characterId;
            __instance.MarkRosterDirty();
            // 只回选择者本人（原版为全员广播）：对端收不到确认包就不会把格子
            // 置灰/标记 Selected，也就不会在本地拦截后续的重复选择请求
            player.Session?.Send(new S_PICK_CHARACTER
            {
                CharacterId = characterId,
                PlayerId = playerId
            });
            InvokeCheckPickAllDone(__instance);
            return false;
        }
    }
}
