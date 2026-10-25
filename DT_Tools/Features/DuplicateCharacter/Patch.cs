using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using Data;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.DuplicateCharacter
{
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
            if (!t.Field("_pickReady").GetValue<bool>())
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"忽略选角请求：选角阶段尚未就绪。playerId:{player.PublicInfo.PlayerId}");
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
                    Log.Info<DuplicateCharacterFeature>($"已登记随机选角 - playerId:{playerId}");
                    player.Session?.Send(new S_PICK_CHARACTER
                    {
                        CharacterId = -2,
                        PlayerId = playerId
                    });
                    InvokeCheckPickAllDone(__instance);
                }
                return false;
            }

            if (Managers.Data.CharacterDic.Values
                    .FirstOrDefault((CharacterData x) => x.DataId == characterId) == null)
                return false;

            if (pickPlayers.Contains(playerId) || randomPickPlayers.Contains(playerId))
                return false;

            pickPlayers.Add(playerId);
            pickCharacters.Add(characterId);
            player.CharacterId = characterId;
            __instance.MarkRosterDirty();
            player.Session?.Send(new S_PICK_CHARACTER
            {
                CharacterId = characterId,
                PlayerId = playerId
            });
            InvokeCheckPickAllDone(__instance);
            return false;
        }

        private static readonly MethodInfo MiCheckPickAllDone =
            AccessTools.Method(typeof(GameRoom), "CheckPickAllDone");

        private static void InvokeCheckPickAllDone(GameRoom room)
        {
            MiCheckPickAllDone?.Invoke(room, null);
        }
    }

    [HarmonyPatch(typeof(GameRoom), "PickRandomOwnedCharacter")]
    internal static class DuplicateCharacterRandomPickPatch
    {
        private static readonly HashSet<int> EmptyTaken = new HashSet<int>();

        private static void Prefix(ref HashSet<int> taken)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
                return;

            taken = EmptyTaken;
        }
    }
}
