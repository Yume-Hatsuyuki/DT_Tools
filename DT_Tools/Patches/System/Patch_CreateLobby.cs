using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using DummyClient;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using Steamworks;
using UnityEngine;
using GamePlayer = Server.Game.Player;

namespace DT_Tools.Patches.System
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   SteamLobbyManager::CreateLobby(...)
    ///   GameRoom::HandleEnterPlayer(...)
    ///
    /// <b>0.1.13h 原版行为</b>：
    ///   CreateLobby 硬编码 SteamMatchmaking.CreateLobby(..., 16)
    ///   （STEAM_LOBBY_MEMBER_LIMIT = 16；MaxMembers 常量仍为 8 但已不用）。
    ///   房间列表 UI 写死 Max = 8，不再读 GetLobbyMemberLimit。
    ///   进房判定：RoomMemberCountForMetadata() >= 8 → ErrorRoomFull。
    ///   Steam 容器 16 与游戏可进人数 8 是拆开的。
    ///
    /// <b>修改后效果</b>：
    ///   [CreateLobby].MaxMembers（默认 8）：游戏进房人数上限。
    ///   [CreateLobby].SteamMemberLimit（默认 16）：Steam CreateLobby 容器上限。
    ///   建房时若 SteamMemberLimit < MaxMembers，按 MaxMembers 抬高，避免进房上限被 Steam 卡住。
    ///   座位表 ObjectUtils 仍为 16 格，两项均限制在 1–16。
    ///
    /// <b>0.1.14b 新特性</b>：
    ///   客户端入房新增 ACK 看门狗（5 秒未应答自动重发 C_ENTER_GAME，共 2 次）；
    ///   服务端 HandleEnterPlayer 开头新增重复入房守卫（session 已绑定非 Dummy 玩家则直接忽略）。
    ///   本补丁已同步该守卫，否则重发包会被误判为 ErrorDuplicateAccount 而踢人。
    ///
    /// <b>修改方式</b>：
    ///   CreateLobby / HandleEnterPlayer 均为 Prefix，按原版 C# 逻辑重写，只替换两处上限来源；
    ///   HandleEnterPlayer 保留 0.1.14b 的重复入房守卫。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "CreateLobby",
        "房间人数：MaxMembers 是游戏进房上限（默认 8）；SteamMemberLimit 是 Steam 容器（默认 16）。",
        author: "梦初雪")]
    internal static class Patch_CreateLobby
    {
        private static ConfigEntry<int> _maxMembers;
        private static ConfigEntry<int> _steamMemberLimit;

        /// <summary>游戏进房人数上限（未绑定时回退 8）。</summary>
        internal static int MaxMembers =>
            Math.Clamp(_maxMembers?.Value ?? 8, 1, 16);

        /// <summary>配置里的 Steam 容器上限（未绑定时回退 16）。</summary>
        internal static int SteamMemberLimit =>
            Math.Clamp(_steamMemberLimit?.Value ?? 16, 1, 16);

        /// <summary>实际用于 CreateLobby 的值：至少不小于进房上限。</summary>
        internal static int EffectiveSteamMemberLimit =>
            Math.Clamp(Math.Max(SteamMemberLimit, MaxMembers), 1, 16);

        static Patch_CreateLobby()
        {
            _maxMembers = Plugin.Instance.Config.Bind(
                "CreateLobby",
                "MaxMembers",
                8,
                new ConfigDescription(
                    "游戏进房人数上限（1–16）。0.1.13h 原版为 8。",
                    new AcceptableValueRange<int>(1, 16)));

            _steamMemberLimit = Plugin.Instance.Config.Bind(
                "CreateLobby",
                "SteamMemberLimit",
                16,
                new ConfigDescription(
                    "Steam Lobby 容器上限（1–16）。0.1.13h 原版 CreateLobby 为 16。" +
                    "若小于 MaxMembers，建房时会抬到 MaxMembers。",
                    new AcceptableValueRange<int>(1, 16)));
        }

        [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.CreateLobby))]
        [HarmonyPrefix]
        private static bool PrefixCreateLobby(
            SteamLobbyManager __instance,
            string roomCode,
            string roomName,
            bool isPrivate,
            string mic,
            string lang,
            Action<bool> onComplete)
        {
            var t = Traverse.Create(__instance);

            t.Field("_pendingRoomCode").SetValue(roomCode);
            t.Field("_pendingRoomName").SetValue(roomName);
            t.Field("_pendingIsPrivate").SetValue(isPrivate);
            t.Field("_pendingMic").SetValue(mic);
            t.Field("_pendingLang").SetValue(lang);
            t.Field("_pendingCreateCallback").SetValue(onComplete);
            t.Field("_createEnterPending").SetValue(true);

            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, EffectiveSteamMemberLimit);
            return false;
        }

        // 按原版 HandleEnterPlayer 重写，仅把「>= 8」换成 MaxMembers；
        // 并保留 0.1.14b 方法开头的重复入房守卫。
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.HandleEnterPlayer))]
        [HarmonyPrefix]
        private static bool PrefixHandleEnterPlayer(GameRoom __instance, HostPeerSession session, C_ENTER_GAME pkt)
        {
            // 0.1.14b 新增：客户端入房 ACK 看门狗会在 5 秒未应答时重发 C_ENTER_GAME，
            // 已绑定真实（非 Dummy）玩家的会话再次入房直接忽略，避免被误判为重复账号而踢出。
            if (session.Player != null && !session.Player.IsDummy)
            {
                Debug.Log($"[Host] Duplicate enter ignored pid={session.Player.PublicInfo.PlayerId} name={pkt.PlayerName}");
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
                Debug.Log($"[Host] Reject enter: steamId already left lobby ({session.SteamId})");
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
            else if ((!reclaimDummy && __instance.RoomMemberCountForMetadata() >= MaxMembers) || !ObjectUtils.HasFreeSeat())
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
                Debug.Log("[Host] Build mismatch on join: guest='" + guestBuild + "' vs host='" + hostBuild + "'");
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

            Debug.Log($"[Host] Enter decision name={pkt.PlayerName} success={s_ENTER_GAME.Success} reason={s_ENTER_GAME.Name} players={__instance.Players.Count} members={__instance.RoomMemberCountForMetadata()} state={__instance.State} spectating={spectating}");
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
            Debug.Log($"[Tracking][Lifecycle] Host enter pid={player.PublicInfo.PlayerId} name={player.Name} state={__instance.State} playersCount={__instance.Players.Count}");
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

        private static void InvokeEnterSpectator(GameRoom room, GamePlayer player)
        {
            AccessTools.Method(typeof(GameRoom), "EnterSpectator").Invoke(room, new object[] { player });
        }
    }
}