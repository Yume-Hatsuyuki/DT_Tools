using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using Data;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.DuplicateCharacter
{
    /// <summary>
    /// GameRoom.PickCharacter 整替（0.1.15b GameRoom.cs:1717）：去掉角色唯一性闸门，
    /// 其余与原版一致；确认包只回选择者本人（原版为全员广播——对端收不到确认包
    /// 就不会把格子置灰/标记 Selected，也就不会在本地拦截后续的重复选择请求）。
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.PickCharacter))]
    internal static class DuplicateCharacterPickCharacterPatch
    {
        private static bool Prefix(GameRoom __instance, Server.Game.Player player, int characterId)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
                return true;

            if (__instance.State != EGameState.PickCharacter)
                return false;

            var t = Traverse.Create(__instance);
            // _pickReady 为私有字段：0.1.15b GameRoom.cs:97
            if (!t.Field("_pickReady").GetValue<bool>())
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"PickCharacter ignored - phase not ready yet. playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            if (TimeManager.Instance.StopWatch >= 40)
                return false;

            // 私有字段：_pickPlayers:91 / _pickCharacters:93 / _randomPickPlayers:95（0.1.15b GameRoom.cs）
            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();
            var randomPickPlayers = t.Field("_randomPickPlayers").GetValue<HashSet<int>>();

            int playerId = player.PublicInfo.PlayerId;

            if (characterId == -2)
            {
                if (!pickPlayers.Contains(playerId) && randomPickPlayers.Add(playerId))
                {
                    Log.Info<DuplicateCharacterFeature>($"Random pick reserved - playerId:{playerId}");
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
            // 只回选择者本人（原版为全员广播）
            player.Session?.Send(new S_PICK_CHARACTER
            {
                CharacterId = characterId,
                PlayerId = playerId
            });
            InvokeCheckPickAllDone(__instance);
            return false;
        }

        // CheckPickAllDone 为私有方法，触发全员锁定：0.1.15b GameRoom.cs:1769。
        // 每次选角确认都会走到这里：MethodInfo 进程内不变，static readonly 缓存一次；
        // AccessTools 未命中返回 null（缓存即失败标记，不再重查），调用处 ?. 保持原语义。
        private static readonly MethodInfo MiCheckPickAllDone =
            AccessTools.Method(typeof(GameRoom), "CheckPickAllDone");

        private static void InvokeCheckPickAllDone(GameRoom room)
        {
            MiCheckPickAllDone?.Invoke(room, null);
        }
    }

    /// <summary>
    /// 超时补选 / 进房随机分配（原版 private static int PickRandomOwnedCharacter(Player, HashSet&lt;int&gt;)，
    /// 0.1.15b GameRoom.cs:1669）：不排除已被占用的角色。
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), "PickRandomOwnedCharacter")]
    internal static class DuplicateCharacterRandomPickPatch
    {
        // BuildPickCandidates 只读 taken（Contains），共享一个空集即可，避免每次分配新对象
        private static readonly HashSet<int> EmptyTaken = new HashSet<int>();

        private static void Prefix(ref HashSet<int> taken)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
                return;

            taken = EmptyTaken;
        }
    }
}
