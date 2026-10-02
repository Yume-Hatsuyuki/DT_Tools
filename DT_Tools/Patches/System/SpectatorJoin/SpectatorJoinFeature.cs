using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DummyClient;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Patches.System.LobbyMaxPlayers;

namespace DT_Tools.Patches.System.SpectatorJoin
{
    /// <summary>
    /// 满房观战：等待中（大厅）与进行中（Survive/Detective/允许观战的审判）的满房房间均可加入观战。
    /// 等待中加入者在房间（Lobby）内保持存活、可见并可自由移动，不占房间人数；
    /// 游戏开始（PickCharacter → Survive）后由服务端切换为幽灵观战模式；
    /// 进行中加入则直接进入幽灵观战模式。均不影响局内其他人的对局体验
    /// （不占人数、不参与角色/黑方分配、不参与开局判定）。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "满房观战：等待中（大厅）与进行中（Survive/Detective/允许观战的审判）的满房房间均可加入观战，观战者不占人数、不影响他人对局。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Author = "合理")]
    public sealed partial class SpectatorJoinFeature
    {
        [Config("同时观战人数上限（1–16）。实际还受 Steam 大厅 16 人总上限约束。", Min = 1, Max = 16)]
        public static int MaxSpectators = 8;

        internal static int MaxSpectatorsValue =>
            Math.Clamp(MaxSpectators, 1, 16);

        internal static int SpectatorCount(GameRoom room) =>
            room.Players.Count(p => p.IsSpectator);

        private static bool InvokeIsSamePairIdentity(GamePlayer a, GamePlayer b)
        {
            MethodInfo m = AccessTools.Method(typeof(GameRoom), "IsSamePairIdentity");
            return m != null && (bool)m.Invoke(null, new object[] { a, b });
        }

        // ── 房间成员数：观战者不计入（房间列表显示与满员判断只反映真实玩家） ──
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.RoomMemberCountForMetadata))]
        [HarmonyPrefix]
        private static bool PrefixRoomMemberCountForMetadata(GameRoom __instance, ref int __result)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            int dup = __instance.Players.Count(d =>
                !d.IsSpectator && d.IsDummy &&
                __instance.Players.Any(p => !p.IsSpectator && !p.IsDummy && InvokeIsSamePairIdentity(d, p)));
            __result = __instance.Players.Count(p => !p.IsSpectator) - dup;
            return false;
        }

        // ── 开局判定：观战者无需准备、不计入人数 ────────────────────────────
        [HarmonyPatch(typeof(GameRoom), "CheckAllPlayerReady")]
        [HarmonyPrefix]
        private static bool PrefixCheckAllPlayerReady(GameRoom __instance, ref bool __result)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            foreach (GamePlayer player in __instance.Players)
            {
                if (player != __instance.Host && !player.IsSpectator && !player.Ready)
                {
                    __result = false;
                    return false;
                }
            }
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.HandleStart))]
        [HarmonyPrefix]
        private static bool PrefixHandleStart(GameRoom __instance, GamePlayer player)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            int realCount = __instance.Players.Count(p => !p.IsSpectator);
            if (__instance.State == EGameState.Lobby
                && __instance.Host != null
                && __instance.Host.PublicInfo.PlayerId == player.PublicInfo.PlayerId
                && realCount >= Define.LOBBY_MIN_PLAYER
                && __instance.Players.All(p => p == __instance.Host || p.IsSpectator || p.Ready))
            {
                __instance.BroadcastSystemSFX(ESoundType.ElevatorSfx);
                __instance.ChangeGameState(EGameState.PickCharacter);
            }
            return false;
        }

        // ── 选角阶段：观战者不参与选角、不占用角色 ──────────────────────────
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.PickCharacter))]
        [HarmonyPrefix]
        private static bool PrefixPickCharacter(GameRoom __instance, GamePlayer player, int characterId)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            if (player != null && player.IsSpectator)
            {
                Log.Info<SpectatorJoinFeature>($"忽略观战者选择角色 pid={player.PublicInfo.PlayerId}");
                return false;
            }
            return true;
        }

        // 全员选完判定：只统计真实玩家，避免观战者拖慢选角倒计时
        [HarmonyPatch(typeof(GameRoom), "CheckPickAllDone")]
        [HarmonyPrefix]
        private static bool PrefixCheckPickAllDone(GameRoom __instance)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            var t = Traverse.Create(__instance);
            List<int> pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            HashSet<int> randomPickPlayers = t.Field("_randomPickPlayers").GetValue<HashSet<int>>();
            int realCount = __instance.Players.Count(p => !p.IsSpectator);
            if ((pickPlayers?.Count ?? 0) + (randomPickPlayers?.Count ?? 0) >= realCount)
            {
                TimeManager.Instance.SetStopWatch(40);
            }
            return false;
        }

        // 超时补选：PickCharacter 阶段跳过观战者（进房 Lobby 分配角色仍走原版）
        [HarmonyPatch(typeof(GameRoom), "PickRandomOwnedCharacter")]
        [HarmonyPrefix]
        private static bool PrefixPickRandomOwnedCharacter(GamePlayer player, ref int __result)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            if (player != null && player.IsSpectator && GameRoom.Instance.State == EGameState.PickCharacter)
            {
                __result = 0;
                return false;
            }
            return true;
        }

        // ── 客户端：满房大厅观战 —— 保持存活、可见、可自由移动 ──────────────
        // 原版对 IsSpectator=true 的加入一律执行 Dead()（隐藏、锁定），
        // 那只适合"局中加入"。等待阶段的观战者应像普通大厅成员一样活动，
        // 到游戏开始时由服务端 S_DEAD 切换为幽灵观战。
        [HarmonyPatch]
        private static class EnterGameAckPatch
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_ENTER_GAME");

            [HarmonyPrefix]
            private static bool Prefix(IPacketSink session, Packet packet)
            {
                if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                    return true;

                if (!(packet?.Pkt is S_ENTER_GAME pkt))
                    return true;

                // 客户端已开启观战功能但被房主拒绝（满员/无座位）→ 给出可操作的提示，
                // 让用户知道需要房主开启 [SpectatorJoin]（或受 Steam 16 人上限限制）。
                if (!pkt.Success && pkt.Name == "ErrorRoomFull")
                {
                    Managers.Network.OccurError = true;
                    Managers.Network.ErrorMessage =
                        "房间已满。以观战身份加入需房主在 DT CONFIG 开启 [SpectatorJoin]；16 人满房受 Steam 上限限制无法加入";
                    Managers.Network.Leave();
                    Managers.Scene.LoadScene(Define.EScene.LobbyScene);
                    return false;
                }

                if (!pkt.IsSpectator || pkt.GameState != EGameState.Lobby)
                    return true;

                Managers.Network.NotifyEnterAck();
                Managers.Network.OccurError = false;
                Managers.Player.Spawn(pkt);
                AwardHistory.Clear();
                Managers.Game.IsSpectator = true;
                Managers.Game.State = EGameState.Lobby;
                Managers.Game.BroadcastSceneEvent(Define.ESceneEventType.HideAll);
                Managers.Game.BroadcastSceneEvent(Define.ESceneEventType.ShowUI, 1);
                Managers.Voice.JoinChannelLogin();
                return false;
            }
        }

        // ── 客户端：观战者死后保持自由移动（幽灵飞行） ─────────────────────
        // 游戏开始时服务端下发 S_DEAD，Dead() 会把 CanControl 置 false；
        // 观战者需要像幽灵一样继续自由移动。
        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Dead))]
        [HarmonyPostfix]
        private static void PostfixDead(GameManagerEX __instance)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return;

            if (__instance.IsSpectator)
                __instance.CanControl = true;
        }

        // ── 客户端：观战者不显示选角弹窗（也不发送选角包） ─────────────────
        [HarmonyPatch(typeof(GameManagerEX), "StartPick")]
        [HarmonyPrefix]
        private static bool PrefixStartPickClient(GameManagerEX __instance)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            if (__instance.IsSpectator)
                return false;
            return true;
        }
    }
}
