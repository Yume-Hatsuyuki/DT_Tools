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
using DT_Tools.Features.LobbyMaxPlayers;

namespace DT_Tools.Features.SpectatorJoin
{
    [HarmonyPatch]
    [PatchFeature(
        "满房观战：等待中（大厅）与进行中（Survive/Detective/允许观战的审判）的满房房间均可加入观战，观战者不占人数、不影响他人对局。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.SysRoom | FeatureTag.GameRound,
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

        private static readonly List<int> _readyOrder = new List<int>();

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

            _readyOrder.RemoveAll(pid2 => !__instance.Players.Any(p =>
                p.PublicInfo.PlayerId == pid2 && !p.IsSpectator));
        }

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
                sp.IsSpectator = true;
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
                ConvertExcessToSpectators(__instance);
                __instance.BroadcastSystemSFX(ESoundType.ElevatorSfx);
                __instance.ChangeGameState(EGameState.PickCharacter);
            }
            return false;
        }

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

        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.ResumeTickAfterMigration))]
        [HarmonyPostfix]
        private static void PostfixResumeTickAfterMigration(GameRoom __instance)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return;
            if (__instance == null || __instance.IsMigrating)
                return;
            var spectators = __instance.Players
                .Where(p => p != null && p.IsSpectator && p.Session != null).ToList();
            foreach (var p in spectators)
            {
                p.ChangeArea(p.PublicInfo.Pos, force: true);
            }
            if (spectators.Count > 0)
            {
                Log.Info<SpectatorJoinFeature>(
                    $"迁移恢复：已向 {spectators.Count} 名观战者补发区域包");
            }
        }

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

        [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Dead))]
        [HarmonyPostfix]
        private static void PostfixDead(GameManagerEX __instance)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return;

            if (__instance.IsSpectator)
                __instance.CanControl = true;
        }

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

        [HarmonyPatch]
        private static class MapChangeAreaPatch
        {
            static MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.TypeByName("MapManager"), "ChangeArea",
                    new[] { typeof(S_AREA_PUBLIC) });

            [HarmonyPrefix]
            private static bool Prefix(MapManager __instance, S_AREA_PUBLIC pkt)
            {
                if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                    return true;
                if (pkt == null || pkt.RoomId == 0)
                    return true;
                if (!Managers.Game.IsSpectator)
                    return true;

                var cur = __instance.CurrentArea;
                if (cur != null && cur.AreaInfo != null && cur.AreaInfo.RoomId != pkt.RoomId)
                {
                    Traverse.Create(__instance).Property("CurrentArea").SetValue(null);
                    Log.Info<SpectatorJoinFeature>(
                        $"观战者区域切换 {cur.AreaInfo.RoomId} → {pkt.RoomId}，强制重新加载地图");
                }
                return true;
            }
        }

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
