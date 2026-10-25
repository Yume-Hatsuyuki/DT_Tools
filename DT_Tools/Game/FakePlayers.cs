using System;
using System.Collections.Generic;
using System.Linq;
using DummyClient;
using Google.Protobuf;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using Steamworks;

namespace DT_Tools.Game
{
    public static class FakePlayers
    {
        public const int RandomCharacterId = -2;

        private sealed class FakePacketSink : IPacketSink
        {
            public void Send(IMessage packet)
            {
            }
        }

        private static readonly List<Server.Game.Player> Created = new List<Server.Game.Player>();

        public static bool TryCreate(string rawName, int characterId, int capacity, out Server.Game.Player created, out string error, out string text)
        {
            created = null;
            error = null;
            text = null;

            if (!HostGuard.IsHost)
            {
                error = "host only";
                text = "只有房主才能创建假人。";
                return false;
            }
            var room = GameRoom.Instance;
            if (room == null || room.Host == null)
            {
                error = "no room";
                text = "尚未进入房间（需要先创建/进入联机大厅）。";
                return false;
            }
            if (room.State != EGameState.Lobby)
            {
                error = "lobby only";
                text = $"当前阶段 {room.State}，只有回到大厅才能创建假人。";
                return false;
            }

            Prune(room);

            if (room.Players.Count >= capacity)
            {
                error = "room full";
                text = $"房间已满（{capacity} 人），无法再加入假人。";
                return false;
            }
            if (!ObjectUtils.HasFreeSeat())
            {
                error = "room full";
                text = "没有空闲座位，无法再加入假人（座位表上限 16）。";
                return false;
            }
            if (!PlayerName.TrySanitize(rawName, out string name, out string nameError))
            {
                error = "invalid name";
                text = nameError;
                return false;
            }
            if (room.Players.Any(p => p != null && p.Name == name))
            {
                error = "duplicate name";
                text = $"房间内已有人叫「{name}」，换一个昵称。";
                return false;
            }

            var session = new HostPeerSession(new FakePacketSink())
            {
                PlayerID = name,
                SteamId = new CSteamID(0),
            };
            var player = ObjectUtils.CreatePlayer(session, name);
            if (player.PublicInfo.PlayerId <= 0)
            {
                error = "room full";
                text = "座位分配失败（ID -1），无法创建假人。";
                return false;
            }

            try
            {
                if (characterId != 0 && characterId != RandomCharacterId)
                {
                    if (Managers.Data?.CharacterDic == null || !Managers.Data.CharacterDic.ContainsKey(characterId))
                    {
                        error = "unknown character";
                        text = $"未知角色 ID {characterId}。";
                        return false;
                    }
                    player.CharacterId = characterId;
                }
                else if (characterId == RandomCharacterId)
                {
                    player.CharacterId = RandomFreeCharacter(room, player);
                }

                room.Players.Add(player);
                room.MarkRosterDirty();

                player.InitLobby();

                var addPacket = new S_ADD_PLAYER
                {
                    PlayerId = player.PublicInfo.PlayerId,
                    Name = player.Name,
                    AccountId = player.AccountID,
                    CharacterId = player.PublicInfo.CharacterId,
                };
                foreach (var other in room.Players)
                {
                    if (other == player || other.Session == null)
                        continue;
                    other.Session.Send(addPacket);
                }

                foreach (var other in room.Players)
                {
                    if (other == player || other.Session == null)
                        continue;
                    player.AddPlayer(other);
                    other.AddPlayer(player);
                }

                player.ConvertToDummy();

                Created.Add(player);
                text = $"假人「{name}」已进场（#{player.PublicInfo.PlayerId}）。";
                return true;
            }
            catch (Exception ex)
            {
                try { room.HandleLeavePlayer(player.Session, player); }
                catch { }
                Created.Remove(player);
                Log.Exception("FakePlayer", ex, $"创建假人「{name}」失败");
                error = "create failed";
                text = $"创建假人「{name}」失败：{ex.Message}";
                return false;
            }
        }

        public static bool TryRemove(string nameOrId, out string error, out string text)
        {
            error = null;
            text = null;
            if (!Resolve(nameOrId, out var room, out var player, out error, out text))
                return false;

            room.HandleLeavePlayer(player.Session, player);
            Created.Remove(player);
            text = $"假人「{player.Name}」已移出。";
            return true;
        }

        public static int RemoveAll()
        {
            var room = GameRoom.Instance;
            if (room == null)
                return 0;
            Prune(room);
            int removed = 0;
            foreach (var player in Created.ToList())
            {
                try
                {
                    room.HandleLeavePlayer(player.Session, player);
                    Created.Remove(player);
                    removed++;
                }
                catch (Exception ex)
                {
                    Log.Exception("FakePlayer", ex, $"移除假人「{player.Name}」失败");
                }
            }
            return removed;
        }

        public static bool TrySetReady(string nameOrId, bool ready, out string error, out string text)
        {
            error = null;
            text = null;
            if (!Resolve(nameOrId, out var room, out var player, out error, out text))
                return false;
            if (room.State != EGameState.Lobby)
            {
                error = "lobby only";
                text = "只有大厅阶段可以改变准备状态。";
                return false;
            }
            room.HandleReady(player, ready);
            text = $"假人「{player.Name}」{(ready ? "已准备" : "已取消准备")}。";
            return true;
        }

        public static bool TryPick(string nameOrId, int characterId, out string error, out string text)
        {
            error = null;
            text = null;
            if (!Resolve(nameOrId, out var room, out var player, out error, out text))
                return false;

            if (room.State == EGameState.PickCharacter)
            {
                if (characterId != RandomCharacterId &&
                    (Managers.Data?.CharacterDic == null || !Managers.Data.CharacterDic.ContainsKey(characterId)))
                {
                    error = "unknown character";
                    text = $"未知角色 ID {characterId}。";
                    return false;
                }
                room.PickCharacter(player, characterId);

                var picked = AccessTools.Field(typeof(GameRoom), "_pickPlayers")?.GetValue(room) as List<int>;
                var randomPicked = AccessTools.Field(typeof(GameRoom), "_randomPickPlayers")?.GetValue(room) as HashSet<int>;
                bool locked = characterId == RandomCharacterId
                    ? randomPicked != null && randomPicked.Contains(player.PublicInfo.PlayerId)
                    : picked != null && picked.Contains(player.PublicInfo.PlayerId);
                if (!locked)
                {
                    error = "pick not applied";
                    text = characterId == RandomCharacterId
                        ? $"假人「{player.Name}」的随机选角未生效（可能已锁定过）。"
                        : $"角色 {characterId} 已被占用或已锁定，假人「{player.Name}」选角未生效。";
                    return false;
                }
                text = characterId == RandomCharacterId
                    ? $"假人「{player.Name}」已锁定随机选角。"
                    : $"假人「{player.Name}」已选择角色 {characterId}。";
                return true;
            }
            if (room.State != EGameState.Lobby)
            {
                error = "wrong state";
                text = "只有大厅或选角阶段可以为假人选角色。";
                return false;
            }

            if (player.Ready)
                room.HandleReady(player, false);

            if (characterId == RandomCharacterId)
                characterId = RandomFreeCharacter(room, player);
            if (Managers.Data?.CharacterDic == null || !Managers.Data.CharacterDic.ContainsKey(characterId))
            {
                error = "unknown character";
                text = $"未知角色 ID {characterId}。";
                return false;
            }
            player.ModifyPlayer(new C_MODIFY_PLAYER
            {
                Type = EModifyPlayerEvent.ChangeCharacter,
                Value = characterId,
            });
            text = $"假人「{player.Name}」已换用角色 {characterId}。";
            return true;
        }

        public static object Snapshot(int capacity)
        {
            var room = GameRoom.Instance;
            if (room == null)
            {
                return new { isHost = HostGuard.IsHost, gameState = "NoneState", capacity, players = Array.Empty<object>() };
            }

            Prune(room);
            return new
            {
                isHost = HostGuard.IsHost,
                gameState = room.State.ToString(),
                capacity,
                players = room.Players
                    .OrderBy(p => p.PublicInfo.PlayerId)
                    .Select(p => new
                    {
                        playerId = p.PublicInfo.PlayerId,
                        name = p.Name,
                        characterId = p.PublicInfo.CharacterId,
                        ready = p.Ready,
                        isDummy = p.IsDummy,
                        ours = Created.Contains(p),
                        isHost = room.Host == p,
                        alive = p.IsAlive,
                        spectator = p.IsSpectator,
                    })
                    .ToArray(),
            };
        }

        private static int Prune(GameRoom room)
        {
            return Created.RemoveAll(p => p == null || !room.Players.Contains(p));
        }

        private static bool Resolve(string nameOrId, out GameRoom room, out Server.Game.Player player, out string error, out string text)
        {
            player = null;
            error = null;
            text = null;
            room = null;

            if (!HostGuard.IsHost)
            {
                error = "host only";
                text = "只有房主才能操作假人。";
                return false;
            }
            room = GameRoom.Instance;
            if (room == null || room.Host == null)
            {
                error = "no room";
                text = "尚未进入房间。";
                return false;
            }

            Prune(room);
            string key = (nameOrId ?? string.Empty).Trim();
            if (key.StartsWith("#"))
                key = key.Substring(1);
            if (int.TryParse(key, out int seatId))
                player = Created.FirstOrDefault(p => p.PublicInfo.PlayerId == seatId);
            if (player == null)
                player = Created.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.Ordinal));
            if (player == null)
            {
                error = "unknown dummy";
                text = $"找不到假人「{nameOrId}」（可能已被游戏清场）。";
                return false;
            }
            return true;
        }

        private static int RandomFreeCharacter(GameRoom room, Server.Game.Player self)
        {
            var taken = new HashSet<int>();
            foreach (var p in room.Players)
            {
                if (p != null && p != self && p.PublicInfo != null)
                    taken.Add(p.PublicInfo.CharacterId);
            }

            var dic = Managers.Data?.CharacterDic;
            if (dic == null || dic.Count == 0)
                return 0;

            var candidates = dic.Values
                .Where(d => d != null && !taken.Contains(d.DataId) && d.Type != ECharacterType.Madeline)
                .Select(d => d.DataId)
                .ToList();
            if (candidates.Count == 0)
                candidates = dic.Keys.Where(id => !taken.Contains(id)).ToList();
            if (candidates.Count == 0)
                candidates = dic.Keys.ToList();
            return candidates[Util.GetRandomNumber(0, candidates.Count)];
        }
    }
}
