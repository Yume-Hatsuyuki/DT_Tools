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

        [Config("开局参与人数上限（1–16，默认 8 = 原版）。当房间真实玩家数超过该值时，开局前自动将最后准备的人转为观战者（按准备倒序，房主除外）。", Min = 1, Max = 16)]
        public static int PlayMaxPlayersEntry = 8;

        internal static int MaxSpectatorsValue =>
            Math.Clamp(MaxSpectators, 1, 16);

        internal static int PlayMaxPlayersValue =>
            Math.Clamp(PlayMaxPlayersEntry, 1, 16);

        internal static int SpectatorCount(GameRoom room) =>
            room.Players.Count(p => p.IsSpectator);

        /// <summary>最后准备顺序（PlayerId 按准备时间升序；转观战时取倒序 = 最后准备者优先）。</summary>
        private static readonly List<int> _readyOrder = new List<int>();

        /// <summary>
        /// 记录准备顺序：每次有人点准备（isReady=true）即移到列表末尾（=最后准备），
        /// 取消准备（isReady=false）则移除。开局前由转观战逻辑按倒序选取"最后准备者"。
        /// </summary>
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.HandleReady))]
        [HarmonyPostfix]
        private static void PostfixHandleReady(GameRoom __instance, GamePlayer player, bool isReady)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return;
            if (player?.PublicInfo == null)
                return;

            int pid = player.PublicInfo.PlayerId;
            if (isReady)
            {
                _readyOrder.Remove(pid);
                _readyOrder.Add(pid);
            }
            else
            {
                _readyOrder.Remove(pid);
            }

            // 清理已离开房间 / 已转观战 / 房主的旧记录（房主转观战时排除）
            _readyOrder.RemoveAll(pid2 => !__instance.Players.Any(p =>
                p.PublicInfo.PlayerId == pid2 && !p.IsSpectator));
        }

        /// <summary>
        /// 超员转观战：真实玩家数超过开局参与上限时，按"最后准备倒序"（房主除外）
        /// 将多余玩家转为观战者，并置脏 roster 同步全员。此逻辑在房主点开始
        /// （全员已准备）时执行，使"谁当观战者"由准备顺序而非进房顺序决定。
        /// </summary>
        private static void ConvertExcessToSpectators(GameRoom room)
        {
            int cap = PlayMaxPlayersValue;
            int real = room.Players.Count(p => !p.IsSpectator);
            if (real <= cap)
                return;

            int need = real - cap;
            List<GamePlayer> candidates = _readyOrder.AsEnumerable().Reverse()
                .Where(pid => room.Players.Any(p => p.PublicInfo.PlayerId == pid
                    && !p.IsSpectator && p != room.Host))
                .Select(pid => room.Players.First(p => p.PublicInfo.PlayerId == pid))
                .Take(need)
                .ToList();

            foreach (GamePlayer sp in candidates)
            {
                sp.IsSpectator = true;   // 0.1.16b Player.cs:1726 SetSpectator 等价赋值
                real--;
                Log.Info<SpectatorJoinFeature>(
                    $"超员转观战：{sp.Name}(#{sp.PublicInfo.PlayerId})（最后准备者），真实玩家 {real + 1} → {real}（参与上限 {cap}）");
            }

            if (candidates.Count > 0)
                room.MarkRosterDirty();
        }

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
                // 超员转观战：最后准备者转观战（按准备倒序、房主除外），凑够参与上限
                ConvertExcessToSpectators(__instance);
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

        // ── 客户端：已在房间的玩家被转观战（开局前最后准备者）→ 同步观战标记 ──
        // 进房观战通过 S_ENTER_GAME.IsSpectator 通知（EnterGameAckPatch）；
        // 开局前转观战者已在房间内，唯一的变化是服务端 roster 中自己的
        // IsSpectator=true（GameRoom.MarkRosterDirty → S_PLAYER_ROSTER）。
        // 这里在 ApplyRoster 后若发现"自己"被标记观战，则同步 Managers.Game.IsSpectator
        // （此后 StartPick 弹窗跳过、S_DEAD 后幽灵自由移动等现有补丁即自动生效）。
        [HarmonyPatch]
        private static class RosterSpectatorPatch
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.TypeByName("PlayerManager"), "ApplyRoster");

            [HarmonyPostfix]
            private static void Postfix(S_PLAYER_ROSTER pkt)
            {
                if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                    return;
                if (pkt?.Entries == null || Managers.Player?.MyPlayer?.PublicInfo == null)
                    return;

                int me = Managers.Player.MyPlayer.PublicInfo.PlayerId;
                foreach (RosterEntry entry in pkt.Entries)
                {
                    if (entry.PlayerId == me && entry.IsSpectator)
                    {
                        Managers.Game.IsSpectator = true;
                        Log.Info<SpectatorJoinFeature>($"客户端已同步观战标记（局内准备阶段转观战） pid={me}");
                        return;
                    }
                }
            }
        }
    }
}
