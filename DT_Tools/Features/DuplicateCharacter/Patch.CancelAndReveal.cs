using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.DuplicateCharacter
{
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.CancelPickCharacter))]
    internal static class DuplicateCharacterCancelPickPatch
    {
        private static bool Prefix(GameRoom __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
                return true;

            if (__instance.State != EGameState.PickCharacter)
                return false;

            var t = Traverse.Create(__instance);
            if (!t.Field("_pickReady").GetValue<bool>())
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"忽略取消选角：选角阶段尚未就绪。playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            if (TimeManager.Instance.StopWatch >= 40)
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"拒绝取消选角：倒计时已开始。playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();
            var randomPickPlayers = t.Field("_randomPickPlayers").GetValue<HashSet<int>>();

            int playerId = player.PublicInfo.PlayerId;

            if (randomPickPlayers.Remove(playerId))
            {
                Log.Info<DuplicateCharacterFeature>($"已取消随机选角 - playerId:{playerId}");
                player.Session?.Send(new S_CANCEL_PICK_CHARACTER
                {
                    PlayerId = playerId,
                    CharacterId = -2
                });
                return false;
            }

            int index = pickPlayers.IndexOf(playerId);
            if (index == -1)
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"忽略取消选角：该玩家没有选角记录。playerId:{playerId}");
                return false;
            }

            int characterId = pickCharacters[index];
            pickPlayers.RemoveAt(index);
            pickCharacters.RemoveAt(index);
            player.SkillComponent.DeallocateSkill();
            player.CharacterId = 0;
            __instance.MarkRosterDirty();
            Log.Info<DuplicateCharacterFeature>($"取消选角 - playerId:{playerId}/charaId:{characterId}");
            player.Session?.Send(new S_CANCEL_PICK_CHARACTER
            {
                PlayerId = playerId,
                CharacterId = characterId
            });
            return false;
        }
    }

    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.PickCharacterTick))]
    internal static class DuplicateCharacterRevealPatch
    {
        private static bool _revealed;

        private static void Prefix(out int __state)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
            {
                __state = 0;
                return;
            }

            __state = TimeManager.Instance.StopWatch;
        }

        private static void Postfix(GameRoom __instance, int __state)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
                return;

            if (__instance.State != EGameState.PickCharacter)
                return;

            if (__state == 0)
            {
                _revealed = false;
                var players = __instance.Players.ToList();
                foreach (Server.Game.Player receiver in players)
                {
                    foreach (Server.Game.Player other in players)
                    {
                        if (other == receiver)
                            continue;
                        receiver.Session?.Send(new S_PICK_CHARACTER
                        {
                            PlayerId = other.PublicInfo.PlayerId,
                            CharacterId = -2
                        });
                    }
                }
                return;
            }

            if (__state < 40 || _revealed)
                return;

            _revealed = true;
            var t = Traverse.Create(__instance);
            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();

            Log.Info<DuplicateCharacterFeature>($"统一揭晓选角 - 数量:{pickPlayers.Count}");
            for (int i = 0; i < pickPlayers.Count && i < pickCharacters.Count; i++)
            {
                __instance.Broadcast(new S_PICK_CHARACTER
                {
                    PlayerId = pickPlayers[i],
                    CharacterId = pickCharacters[i]
                });
            }
        }
    }
}
