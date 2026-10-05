using System.Collections.Generic;
using DT_Tools.Core;
using Google.Protobuf;
using HarmonyLib;
using Protocol;
using Server.Game;
using Steamworks;
using UnityEngine;

namespace DT_Tools.Patches.Fun.AutoRejoin
{
    /// <summary>
    /// 断线自动重连补丁集合（源自 DT_AutoRejoin v1.0.0，行为逐条保留）：
    /// 1. Handle_S_KICKED：收到房主踢出包 → 标记"本次不自动重连"（尊重房主管理权）；
    /// 2. Handle_S_LEAVE_GAME：被房主移出房间（且是自己）→ 同样不自动重连；
    /// 3. NetworkManager.Leave：仅网络断线（ErrorNetworkUnstable）且非房主时记录房间码；
    /// 4. NetworkManager.EnterRoom：进房成功后清除重连状态；
    /// 5. GameRoom.HandleEnterPlayer（房主侧，HostResume）：对局中掉线玩家重进时恢复原角色
    ///    而非幽灵观战；黑名单玩家不归位、死亡玩家不归位、迁移/过渡中不归位；
    /// 6. Managers.Awake：游戏启动时挂载常驻 Ticker（Update 内按功能开关空转）。
    /// 所有补丁以 Engine.Enabled&lt;AutoRejoinFeature&gt; 为总开关（Ticker/逻辑内检查）。
    /// </summary>
    internal static class Patches
    {
        /// <summary>游戏启动挂载常驻重连 Ticker（功能未启用时空转，无副作用）。</summary>
        [HarmonyPatch(typeof(Managers), "Awake")]
        internal static class AutoRejoinBootPatch
        {
            private static void Postfix()
            {
                AutoRejoinTicker.Ensure();
            }
        }

        /// <summary>收到房主踢出包（S_KICKED）：本次不自动重连。</summary>
        [HarmonyPatch("PacketHandler", "Handle_S_KICKED")]
        internal static class KickedPatch
        {
            private static void Prefix()
            {
                try
                {
                    AutoRejoinState.SuppressedByKick = true;
                    Log.Info<AutoRejoinFeature>("[AutoRejoin] 收到踢出包（S_KICKED），本次不自动重连。");
                    AutoRejoinLogic.ClearRejoin("被房主踢出");
                }
                catch
                {
                }
            }
        }

        /// <summary>被房主移出房间（S_LEAVE_GAME 且玩家是自己）：本次不自动重连。</summary>
        [HarmonyPatch("PacketHandler", "Handle_S_LEAVE_GAME")]
        internal static class RemovedPatch
        {
            private static void Prefix(Packet packet)
            {
                try
                {
                    IMessage pkt = packet.Pkt;
                    S_LEAVE_GAME leave = pkt as S_LEAVE_GAME;
                    MyPlayer my = Managers.Player != null ? Managers.Player.MyPlayer : null;
                    if (leave != null && my != null && leave.PlayerId == my.PublicInfo.PlayerId)
                    {
                        AutoRejoinState.SuppressedByRemoval = true;
                        Log.Info<AutoRejoinFeature>("[AutoRejoin] 被房主移出房间（S_LEAVE_GAME），本次不自动重连。");
                        AutoRejoinLogic.ClearRejoin("被房主移出");
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>仅网络断线（ErrorNetworkUnstable）且非房主时，记录房间码准备自动重连。</summary>
        [HarmonyPatch(typeof(NetworkManager), "Leave")]
        internal static class NetworkLeavePatch
        {
            private static void Prefix(NetworkManager __instance)
            {
                try
                {
                    if (!AutoRejoinState.SuppressedByKick
                        && !AutoRejoinState.SuppressedByRemoval
                        && Engine.Enabled<AutoRejoinFeature>()
                        && AutoRejoinFeature.AutoRejoinEnabled
                        && Managers.Host != null
                        && !Managers.Host.IsHost
                        && __instance.GameServer != null
                        && __instance.OccurError
                        && !(__instance.ErrorMessage != "ErrorNetworkUnstable"))
                    {
                        string roomCode = __instance.RoomCode;
                        if (!string.IsNullOrEmpty(roomCode) && roomCode != "1111111")
                        {
                            AutoRejoinLogic.ArmRejoin(roomCode);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>进房成功后清除重连状态。</summary>
        [HarmonyPatch(typeof(NetworkManager), "EnterRoom")]
        internal static class NetworkEnterRoomPatch
        {
            private static void Postfix()
            {
                AutoRejoinState.SuppressedByKick = false;
                AutoRejoinState.SuppressedByRemoval = false;
                AutoRejoinLogic.ClearRejoin("已进入房间");
            }
        }

        // ===== 房主侧：断线归位（HostResume）=====

        /// <summary>房主在玩家重进时恢复其原角色（dummy→player），而非生成幽灵观战者。</summary>
        [HarmonyPatch(typeof(GameRoom), "HandleEnterPlayer")]
        internal static class HostResumePatch
        {
            private static readonly global::System.Reflection.FieldInfo PendingDisconnects =
                AccessTools.Field(typeof(GameRoom), "_pendingDisconnects");

            private static readonly global::System.Reflection.FieldInfo BannedSteamIds =
                AccessTools.Field(typeof(GameRoom), "_bannedSteamIds");

            private static readonly global::System.Reflection.FieldInfo ScoreBoardPkt =
                AccessTools.Field(typeof(GameRoom), "_score_board_pkt");

            private static readonly global::System.Reflection.MethodInfo SpectatorBootstrap =
                AccessTools.Method(typeof(GameRoom), "SendSpectatorBootstrap", null, null);

            private static bool Prefix(GameRoom __instance, HostPeerSession session, C_ENTER_GAME pkt)
            {
                if (!Engine.Enabled<AutoRejoinFeature>() || !AutoRejoinFeature.HostResumeEnabled)
                {
                    return true;
                }
                try
                {
                    EGameState state = __instance.State;
                    if ((int)state != 3 && (int)state != 4 && (int)state != 5)
                    {
                        return true;
                    }
                    if (session.Player != null && !session.Player.IsDummy)
                    {
                        return true;
                    }
                    if (__instance.IsMigrating || __instance.IsTransitioning)
                    {
                        return true;
                    }
                    if (PendingDisconnects?.GetValue(__instance) is HashSet<CSteamID> pending && pending.Contains(session.SteamId))
                    {
                        return true;
                    }
                    if (BannedSteamIds?.GetValue(__instance) is HashSet<CSteamID> banned && banned.Contains(session.SteamId))
                    {
                        return true;
                    }
                    Server.Game.Player dummy = __instance.Players.Find((Server.Game.Player p) => p.IsDummy && p.Session != null && p.Session.SteamId == session.SteamId);
                    if (dummy == null)
                    {
                        return true;
                    }
                    if (!dummy.IsAlive)
                    {
                        return true;
                    }

                    Log.Warn<AutoRejoinFeature>($"[HostResume] {dummy.Name} 重连归位：dummy→player pid={dummy.PublicInfo.PlayerId} state={state}");
                    session.PlayerID = pkt.PlayerId;
                    dummy.DetachSession(session);
                    session.Player = dummy;
                    dummy.RevertDummy();
                    session.Send(new S_ENTER_GAME
                    {
                        Success = true,
                        Name = dummy.Name,
                        PublicInfo = dummy.PublicInfo,
                        AccountId = dummy.AccountID,
                        Speed = 560f,
                        IsSpectator = false,
                        GameState = state
                    });
                    List<Server.Game.Player> players = __instance.Players;
                    for (int i = 0; i < players.Count; i++)
                    {
                        Server.Game.Player other = players[i];
                        if (other != dummy)
                        {
                            session.Send(new S_ADD_PLAYER
                            {
                                PlayerId = other.PublicInfo.PlayerId,
                                Name = other.Name,
                                AccountId = other.AccountID,
                                CharacterId = other.PublicInfo.CharacterId
                            });
                            session.Send(new S_READY
                            {
                                PlayerId = other.PublicInfo.PlayerId,
                                IsReady = other.Ready
                            });
                        }
                    }
                    session.Send(new S_SET_HOST
                    {
                        HostId = __instance.Host != null ? __instance.Host.PublicInfo.PlayerId : 0
                    });
                    IMessage score = ScoreBoardPkt?.GetValue(__instance) as IMessage;
                    if (score != null)
                    {
                        session.Send(score);
                    }
                    SpectatorBootstrap?.Invoke(__instance, new object[1] { dummy });
                    return false;
                }
                catch (global::System.Exception ex)
                {
                    Log.Warn<AutoRejoinFeature>("[HostResume] 归位失败，回退原版流程：" + ex.Message);
                    return true;
                }
            }
        }
    }
}
