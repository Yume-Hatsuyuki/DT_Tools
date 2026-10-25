using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using Steamworks;

namespace DT_Tools.Features.LobbyMaxPlayers
{
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.HandleEnterPlayer))]
    internal static class LobbyMaxPlayersEnterPlayerPatch
    {
        private static readonly FieldInfo FPendingDisconnects =
            AccessTools.Field(typeof(GameRoom), "_pendingDisconnects");
        private static readonly FieldInfo FBannedSteamIds =
            AccessTools.Field(typeof(GameRoom), "_bannedSteamIds");
        private static readonly FieldInfo FScoreBoardPkt =
            AccessTools.Field(typeof(GameRoom), "_score_board_pkt");
        private static readonly FieldInfo FPendingBootstrapPlayers =
            AccessTools.Field(typeof(GameRoom), "_pendingBootstrapPlayers");
        private static readonly MethodInfo MiHasSameAccountRemnant =
            AccessTools.Method(typeof(GameRoom), "HasSameAccountRemnant");
        private static readonly MethodInfo MiCheckDuplicationName =
            AccessTools.Method(typeof(GameRoom), "CheckDuplicationName");
        private static readonly MethodInfo MiEnterSpectator =
            AccessTools.Method(typeof(GameRoom), "EnterSpectator");
        private static readonly MethodInfo MiSanitizeOwnedCharacters =
            AccessTools.Method(typeof(GameRoom), "SanitizeOwnedCharacters");
        private static readonly MethodInfo MiAssignLobbyCharacter =
            AccessTools.Method(typeof(GameRoom), "AssignLobbyCharacter");
        private static readonly MethodInfo MiEnterPlayer =
            AccessTools.Method(typeof(GameRoom), "EnterPlayer");

        private static bool Prefix(GameRoom __instance, HostPeerSession session, C_ENTER_GAME pkt)
        {
            if (!Engine.Enabled<LobbyMaxPlayersFeature>())
                return true;

            if (session.Player != null && !session.Player.IsDummy)
            {
                Log.Info<LobbyMaxPlayersFeature>(
                    $"忽略重复进房 pid={session.Player.PublicInfo.PlayerId} name={pkt.PlayerName}");
                return false;
            }

            var pendingDisconnects = (HashSet<CSteamID>)FPendingDisconnects.GetValue(__instance);
            var bannedSteamIds = (HashSet<CSteamID>)FBannedSteamIds.GetValue(__instance);
            var pendingBootstrap = (HashSet<int>)FPendingBootstrapPlayers.GetValue(__instance);
            var scoreBoardPkt = (S_SCORE_BOARD)FScoreBoardPkt.GetValue(__instance);

            S_ENTER_GAME s_ENTER_GAME = new S_ENTER_GAME
            {
                Success = true,
                Name = ""
            };
            pkt.PlayerName = Util.NeutralizeRichText(pkt.PlayerName);
            if (pendingDisconnects.Remove(session.SteamId))
            {
                Log.Info<LobbyMaxPlayersFeature>($"拒绝进房：steamId 已离开大厅（{session.SteamId}）");
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorRoomFull";
                session.Send(s_ENTER_GAME);
                __instance.HandleLeavePlayer(session, null);
                return false;
            }

            string guestBuild = SteamMatchmaking.GetLobbyMemberData(
                Managers.Network.Lobby.LobbyId, session.SteamId, "build");
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
            else if ((!reclaimDummy && __instance.RoomMemberCountForMetadata() >= LobbyMaxPlayersFeature.MaxMembers)
                     || !ObjectUtils.HasFreeSeat())
            {
                s_ENTER_GAME.Success = false;
                s_ENTER_GAME.Name = "ErrorRoomFull";
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
                Log.Info<LobbyMaxPlayersFeature>(
                    "进房版本不匹配：guest='" + guestBuild + "' vs host='" + hostBuild + "'");
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

            Log.Info<LobbyMaxPlayersFeature>(
                $"进房判定 name={pkt.PlayerName} success={s_ENTER_GAME.Success} reason={s_ENTER_GAME.Name} " +
                $"players={__instance.Players.Count} members={__instance.RoomMemberCountForMetadata()} " +
                $"state={__instance.State} spectating={spectating}");
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
                foreach (Server.Game.Player item in __instance.Players
                    .Where((Server.Game.Player p) => p.IsDummy && p.Session == session).ToList())
                {
                    item.DetachSession(new HostPeerSession(new SteamP2PSession(session.SteamId))
                    {
                        SteamId = session.SteamId,
                        PlayerID = item.AccountID,
                        Player = item
                    });
                }
            }

            Server.Game.Player player = ObjectUtils.CreatePlayer(session, pkt.PlayerName);
            player.IsSpectator = spectating;
            player.OwnedCharacterIds = InvokeSanitizeOwnedCharacters(pkt.OwnedCharacterIds);
            int assigned = InvokeAssignLobbyCharacter(__instance, player);
            if (assigned != 0)
            {
                player.CharacterId = assigned;
            }
            if (spectating)
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
            if (spectating)
            {
                pendingBootstrap.Add(player.PublicInfo.PlayerId);
            }
            Log.Info<LobbyMaxPlayersFeature>(
                $"生命周期：房主机进房 pid={player.PublicInfo.PlayerId} name={player.Name} " +
                $"state={__instance.State} playersCount={__instance.Players.Count}");
            if (spectating)
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
            foreach (Server.Game.Player player2 in __instance.Players)
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
                    if (!spectating)
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
            if (spectating)
            {
                InvokeEnterSpectator(__instance, player);
            }

            return false;
        }

        private static bool InvokeHasSameAccountRemnant(
            GameRoom room, HostPeerSession session, string accountId, out bool allOwnDummies)
        {
            object[] args = { session, accountId, false };
            bool result = (bool)MiHasSameAccountRemnant.Invoke(room, args);
            allOwnDummies = (bool)args[2];
            return result;
        }

        private static bool InvokeCheckDuplicationName(GameRoom room, string name, HostPeerSession session, string accountId)
        {
            return (bool)MiCheckDuplicationName.Invoke(room, new object[] { name, session, accountId });
        }

        private static List<int> InvokeSanitizeOwnedCharacters(IEnumerable<int> ids)
        {
            return (List<int>)MiSanitizeOwnedCharacters.Invoke(null, new object[] { ids });
        }

        private static int InvokeAssignLobbyCharacter(GameRoom room, Server.Game.Player player)
        {
            return (int)MiAssignLobbyCharacter.Invoke(room, new object[] { player });
        }

        private static void InvokeEnterPlayer(GameRoom room, Server.Game.Player player)
        {
            MiEnterPlayer.Invoke(room, new object[] { player });
        }

        private static void InvokeEnterSpectator(GameRoom room, Server.Game.Player player)
        {
            MiEnterSpectator.Invoke(room, new object[] { player });
        }
    }
}
