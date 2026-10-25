using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DummyClient;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using Steamworks;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;
using DT_Tools.Features.LobbyMaxPlayers;

namespace DT_Tools.Features.SpectatorJoin
{
    public sealed partial class SpectatorJoinFeature
    {
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.HandleEnterPlayer))]
        [HarmonyPrefix]
        private static bool PrefixHandleEnterPlayer(GameRoom __instance, HostPeerSession session, C_ENTER_GAME pkt)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
            {
                if (__instance.RoomMemberCountForMetadata() >= LobbyMaxPlayersFeature.MaxMembers
                    || !ObjectUtils.HasFreeSeat())
                {
                    Log.Info<SpectatorJoinFeature>("满房/无座位但功能未开启，拒绝加入——房主需在 DT CONFIG 开启 [SpectatorJoin]（人数=" +
                        $"{__instance.RoomMemberCountForMetadata()}/{LobbyMaxPlayersFeature.MaxMembers} 空位={ObjectUtils.HasFreeSeat()}）");
                }
                return true;
            }

            int maxMembers = LobbyMaxPlayersFeature.MaxMembers;

            if (session.Player != null && !session.Player.IsDummy)
            {
                Log.Info<SpectatorJoinFeature>($"忽略重复进房 pid={session.Player.PublicInfo.PlayerId} name={pkt.PlayerName}");
                return false;
            }

            var t = Traverse.Create(__instance);
            var pendingDisconnects = t.Field("_pendingDisconnects").GetValue<HashSet<CSteamID>>();
            var bannedSteamIds = t.Field("_bannedSteamIds").GetValue<HashSet<CSteamID>>();
            var pendingBootstrap = t.Field("_pendingBootstrapPlayers").GetValue<HashSet<int>>();
            var scoreBoardPkt = t.Field("_score_board_pkt").GetValue<S_SCORE_BOARD>();

            S_ENTER_GAME s_ENTER_GAME = new S_ENTER_GAME
            {
                Success = true,
                Name = ""
            };
            pkt.PlayerName = Util.NeutralizeRichText(pkt.PlayerName);
            if (pendingDisconnects.Remove(session.SteamId))
            {
                Log.Info<SpectatorJoinFeature>($"拒绝进房：steamId 已离开大厅（{session.SteamId}）");
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorRoomFull";
                session.Send(s_ENTER_GAME);
                __instance.HandleLeavePlayer(session, null);
                return false;
            }

            string guestBuild = SteamMatchmaking.GetLobbyMemberData(Managers.Network.Lobby.LobbyId, session.SteamId, "build");
            string hostBuild = SteamLobbyManager.GetMyBuildId();
            bool buildMismatch = guestBuild != hostBuild;
            bool spectating = false;
            bool hasRemnant = InvokeHasSameAccountRemnant(__instance, session, pkt.PlayerId, out bool allOwnDummies);
            bool reclaimDummy = hasRemnant && allOwnDummies && __instance.State != EGameState.Lobby;

            if (bannedSteamIds.Contains(session.SteamId))
            {
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorKicked";
            }
            else if (hasRemnant && !reclaimDummy)
            {
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorDuplicateAccount";
            }
            else if ((!reclaimDummy && __instance.RoomMemberCountForMetadata() >= maxMembers) || !ObjectUtils.HasFreeSeat())
            {
                bool roomFullByMembers = !reclaimDummy && __instance.RoomMemberCountForMetadata() >= maxMembers;
                bool seatAvailable = ObjectUtils.HasFreeSeat();
                bool stateAllowsSpectate = __instance.State == EGameState.Lobby
                    || __instance.State == EGameState.Survive
                    || __instance.State == EGameState.Detective
                    || (__instance.State == EGameState.Trial && TrialManager.Instance.AcceptsSpectator);
                if (!seatAvailable)
                {
                    Log.Info<SpectatorJoinFeature>($"拒绝：Steam 大厅座位已满（16 上限）人数={__instance.RoomMemberCountForMetadata()}");
                    s_ENTER_GAME.Success = false;
                    s_ENTER_GAME.Name = "ErrorRoomFull";
                }
                else if (__instance.State == EGameState.Lobby)
                {
                    spectating = false;
                    Log.Info<SpectatorJoinFeature>($"Lobby 超员真实进房 name={pkt.PlayerName}（配置上限 {maxMembers}，参与上限 {PlayMaxPlayersValue}，开局时按最后准备顺序转观战）");
                }
                else if (stateAllowsSpectate && SpectatorCount(__instance) < MaxSpectatorsValue)
                {
                    spectating = true;
                    Log.Info<SpectatorJoinFeature>($"局内（state={__instance.State}）观战加入 name={pkt.PlayerName} 观战者={SpectatorCount(__instance)}");
                }
                else
                {
                    Log.Info<SpectatorJoinFeature>($"拒绝：state={__instance.State} 不允许观战或观战者已达上限 {SpectatorCount(__instance)}/{MaxSpectatorsValue}");
                    s_ENTER_GAME.Success = false;
                    s_ENTER_GAME.Name = "ErrorRoomFull";
                }
            }
            else if (__instance.IsMigrating || __instance.IsTransitioning
                || __instance.State == EGameState.NoneState
                || __instance.State == EGameState.PickCharacter
                || __instance.State == EGameState.TotalResult)
            {
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorRoomBusy";
            }
            else if (__instance.State == EGameState.Trial && !TrialManager.Instance.AcceptsSpectator)
            {
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorRoomBusy";
            }
            else if (buildMismatch)
            {
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorBuildMismatch";
                Log.Info<SpectatorJoinFeature>($"进房版本不匹配：guest='{guestBuild}' vs host='{hostBuild}'");
            }
            else if (InvokeCheckDuplicationName(__instance, pkt.PlayerName, session, pkt.PlayerId))
            {
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "DuplicationError";
            }
            else if (__instance.State != EGameState.Lobby)
            {
                spectating = true;
            }

            Log.Info<SpectatorJoinFeature>($"进房决策 name={pkt.PlayerName} success={s_ENTER_GAME.Success} reason={s_ENTER_GAME.Name} players={__instance.Players.Count} members={__instance.RoomMemberCountForMetadata()} state={__instance.State} spectating={spectating}");
            if (!s_ENTER_GAME.Success)
            {
                session.Send(s_ENTER_GAME);
                __instance.HandleLeavePlayer(session, null);
                return false;
            }

            session.PlayerID = pkt.PlayerId;
            SessionManager.Instance.PlayerConnect(pkt.PlayerId);
            if (reclaimDummy)
            {
                foreach (GamePlayer item in __instance.Players.Where((GamePlayer p) => p.IsDummy && p.Session == session).ToList())
                {
                    item.DetachSession(new HostPeerSession(new SteamP2PSession(session.SteamId))
                    {
                        SteamId = session.SteamId,
                        PlayerID = item.AccountID,
                        Player = item
                    });
                }
            }

            GamePlayer player = ObjectUtils.CreatePlayer(session, pkt.PlayerName);
            player.IsSpectator = spectating;
            player.OwnedCharacterIds = InvokeSanitizeOwnedCharacters(pkt.OwnedCharacterIds);
            int assigned = InvokeAssignLobbyCharacter(__instance, player);
            if (assigned != 0)
            {
                player.CharacterId = assigned;
            }
            bool inGame = spectating && __instance.State != EGameState.Lobby;
            if (inGame)
            {
                AreaManager.Instance.CreateInitMapPacket();
            }
            if (spectating && (__instance.State == EGameState.Survive || __instance.State == EGameState.Detective))
            {
                List<PosInfo> list = Managers.Data.MapData.StartPosList.ToList();
                PosInfo posInfo = list[Util.GetRandomNumber(0, list.Count)];
                player.PublicInfo.Pos.X = posInfo.X;
                player.PublicInfo.Pos.Y = posInfo.Y;
                player.EffectivePosition.X = posInfo.X;
                player.EffectivePosition.Y = posInfo.Y;
            }

            InvokeEnterPlayer(__instance, player);
            if (inGame)
            {
                pendingBootstrap.Add(player.PublicInfo.PlayerId);
            }
            Log.Info<SpectatorJoinFeature>($"主机进房 pid={player.PublicInfo.PlayerId} name={player.Name} state={__instance.State} 玩家数={__instance.Players.Count}");
            if (inGame)
            {
                player.PublicInfo.IsGhost = true;
            }

            s_ENTER_GAME.PublicInfo = player.PublicInfo;
            s_ENTER_GAME.Name = player.Name;
            s_ENTER_GAME.AccountId = player.AccountID;
            s_ENTER_GAME.Speed = 560f;
            s_ENTER_GAME.IsSpectator = spectating;
            s_ENTER_GAME.GameState = __instance.State;
            player.Session.Send(s_ENTER_GAME);

            if (__instance.Host == null && !spectating)
            {
                __instance.Host = player;
                SessionManager.Instance.SetInfo(pkt.PlayerCapacity);
            }

            player.InitLobby();
            S_ADD_PLAYER s_ADD_PLAYER = new S_ADD_PLAYER();
            s_ADD_PLAYER.PlayerId = player.PublicInfo.PlayerId;
            s_ADD_PLAYER.Name = player.Name;
            s_ADD_PLAYER.AccountId = player.AccountID;
            s_ADD_PLAYER.CharacterId = player.PublicInfo.CharacterId;
            foreach (GamePlayer player2 in __instance.Players)
            {
                if (player2 != player)
                {
                    S_ADD_PLAYER s_ADD_PLAYER2 = new S_ADD_PLAYER();
                    s_ADD_PLAYER2.PlayerId = player2.PublicInfo.PlayerId;
                    s_ADD_PLAYER2.Name = player2.Name;
                    s_ADD_PLAYER2.AccountId = player2.AccountID;
                    s_ADD_PLAYER2.CharacterId = player2.PublicInfo.CharacterId;
                    player.Session.Send(s_ADD_PLAYER2);
                    player2.Session.Send(s_ADD_PLAYER);
                    if (!spectating || __instance.State == EGameState.Lobby)
                    {
                        player.Session.Send(new S_READY
                        {
                            PlayerId = player2.PublicInfo.PlayerId,
                            IsReady = player2.Ready
                        });
                    }
                }
            }

            player.Session.Send(new S_SET_HOST
            {
                HostId = (__instance.Host?.PublicInfo.PlayerId ?? 0)
            });
            player.Session.Send(scoreBoardPkt);
            if (inGame)
            {
                InvokeEnterSpectatorNow(__instance, player);
                InvokeChangeAreaToGameArea(player);
            }

            return false;
        }

        private static void InvokeEnterSpectatorNow(GameRoom room, GamePlayer player)
        {
            AccessTools.Method(typeof(GameRoom), "EnterSpectator")?.Invoke(room, new object[] { player });
        }

        private static bool InvokeHasSameAccountRemnant(GameRoom room, HostPeerSession session, string accountId, out bool allOwnDummies)
        {
            object[] args = { session, accountId, false };
            bool result = (bool)AccessTools.Method(typeof(GameRoom), "HasSameAccountRemnant").Invoke(room, args);
            allOwnDummies = (bool)args[2];
            return result;
        }

        private static bool InvokeCheckDuplicationName(GameRoom room, string name, HostPeerSession session, string accountId)
        {
            return (bool)AccessTools.Method(typeof(GameRoom), "CheckDuplicationName")
                .Invoke(room, new object[] { name, session, accountId });
        }

        private static List<int> InvokeSanitizeOwnedCharacters(IEnumerable<int> ids)
        {
            return (List<int>)AccessTools.Method(typeof(GameRoom), "SanitizeOwnedCharacters")
                .Invoke(null, new object[] { ids });
        }

        private static int InvokeAssignLobbyCharacter(GameRoom room, GamePlayer player)
        {
            return (int)AccessTools.Method(typeof(GameRoom), "AssignLobbyCharacter")
                .Invoke(room, new object[] { player });
        }

        private static void InvokeEnterPlayer(GameRoom room, GamePlayer player)
        {
            AccessTools.Method(typeof(GameRoom), "EnterPlayer").Invoke(room, new object[] { player });
        }
    }
}
