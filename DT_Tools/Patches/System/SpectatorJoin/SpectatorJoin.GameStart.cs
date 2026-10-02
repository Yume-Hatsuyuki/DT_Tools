using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data;
using HarmonyLib;
using Protocol;
using Server;
using Server.Game;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;

namespace DT_Tools.Patches.System.SpectatorJoin
{
    public sealed partial class SpectatorJoinFeature
    {
        /// <summary>
        /// GameStart：观战者不参与出生点分配，进入幽灵观战模式（EnterSpectator）。
        /// 其余与原版 + LobbyMaxPlayers 对齐：StartPosList 打乱后分配，人数超出
        /// 出生点时 i % Count 循环复用；RoundStartPlayerCount 只统计真实玩家，
        /// 不影响黑方击杀上限。
        /// </summary>
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.GameStart))]
        [HarmonyPrefix]
        private static bool PrefixGameStart(GameRoom __instance)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            Server.Game.ItemManager.Instance.ClearWithDespawn();

            __instance.CorpseMetas.Clear();
            __instance.RoundStartPlayerCount = __instance.Players.Count(p => !p.IsSpectator);
            Log.Info<SpectatorJoinFeature>(
                $"Kill: 回合开始人数 {__instance.RoundStartPlayerCount} → 黑方击杀上限 {__instance.BlackKillLimit} 次 (SpectatorJoin)");

            foreach (GamePlayer player in __instance.Players)
                player.Clear();

            List<PosInfo> list = Managers.Data.MapData.StartPosList != null
                ? Managers.Data.MapData.StartPosList.ToList()
                : new List<PosInfo>();

            if (list.Count == 0)
            {
                PosInfo fallback = Managers.Data.MapData.ErrorPos ?? Managers.Data.MapData.LobbyPos;
                if (fallback == null)
                    fallback = new PosInfo { X = 0f, Y = 0f };
                Log.Warn<SpectatorJoinFeature>("StartPosList 为空，回退 ErrorPos/LobbyPos");
                foreach (GamePlayer player in __instance.Players)
                {
                    if (player.IsSpectator)
                        InvokeEnterSpectator(__instance, player);
                    else
                        player.GameStart(fallback);
                }
            }
            else
            {
                list = Util.Shuffle(list, list.Count);
                int idx = 0;
                foreach (GamePlayer player in __instance.Players)
                {
                    if (player.IsSpectator)
                    {
                        InvokeEnterSpectator(__instance, player);
                        continue;
                    }
                    player.GameStart(list[idx % list.Count]);
                    idx++;
                }

                if (idx > list.Count)
                    Log.Info<SpectatorJoinFeature>($"玩家数={idx} 大于出生点={list.Count}，循环复用");
            }

            Server.Game.DeviceManager.Instance.InitDevices();
            Server.Game.DeviceManager.Instance.InitStorage();

            InvokeMissionStart();

            if (!TryRestoreFirstBlood(__instance, 0))
                Traverse.Create(__instance).Property("FirstBloodVictimId").SetValue(0);

            InvokeAwardStartRound();

            return false;
        }

        /// <summary>
        /// 观战者进入幽灵观战模式：EnterSpectator（幽灵化 + 观战引导包 + 幽灵可见性），
        /// 并向其客户端下发 S_DEAD 完成"大厅存活观战 → 局内幽灵观战"的切换。
        /// </summary>
        private static void InvokeEnterSpectator(GameRoom room, GamePlayer player)
        {
            MethodInfo m = AccessTools.Method(typeof(GameRoom), "EnterSpectator");
            if (m == null)
            {
                Log.Error<SpectatorJoinFeature>("EnterSpectator 不可用，观战者保持大厅状态");
                return;
            }
            m.Invoke(room, new object[] { player });
            player.Session.Send(new S_DEAD());
        }

        /// <summary>
        /// StartPick：观战者不进入存活池、不参与角色颜色分配、不会被随机为黑方/主谋。
        /// 其余与原版一致。
        /// </summary>
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(GameRoom), "StartPick")]
        [HarmonyPrefix]
        private static bool PrefixStartPick(GameRoom __instance)
        {
            if (!Engine.EnabledOf(typeof(SpectatorJoinFeature)))
                return true;

            var t = Traverse.Create(__instance);
            t.Field("_pickReady").SetValue(false);
            t.Field("_pickPlayers").SetValue(new List<int>());
            t.Field("_pickCharacters").SetValue(new List<int>());
            t.Field("_randomPickPlayers").SetValue(new HashSet<int>());

            __instance.SyncAllPlayer(delegate
            {
                SessionManager.Instance.UpdateIsStart(isStart: true);
                __instance.Broadcast(new S_FADE_IN());
                __instance.AlivePlayers.Clear();
                __instance.DeadPlayers.Clear();

                List<GamePlayer> players = __instance.Players.Where(p => !p.IsSpectator).ToList();
                __instance.AlivePlayers.AddRange(players);
                foreach (GamePlayer p in players)
                    p.Color = EPlayerColor.White;
                if (players.Count > 0)
                {
                    int randomNumber = Util.GetRandomNumber(0, players.Count);
                    players[randomNumber].Color = EPlayerColor.Dark;
                    __instance.SetMasterMind(players[randomNumber]);
                }

                t.Field("_pickReady").SetValue(true);
                TimeManager.Instance.ResetStopWatch();
                InvokePickCharacterTick(__instance);
            });

            return false;
        }

        private static void InvokePickCharacterTick(GameRoom room)
        {
            AccessTools.Method(typeof(GameRoom), "PickCharacterTick")?.Invoke(room, null);
        }

        private static bool TryRestoreFirstBlood(GameRoom room, int victimId)
        {
            MethodInfo m = AccessTools.Method(typeof(GameRoom), "RestoreFirstBloodVictimId");
            if (m == null)
                return false;
            m.Invoke(room, new object[] { victimId });
            return true;
        }

        private static void InvokeMissionStart()
        {
            Type mmType = AccessTools.TypeByName("Server.Game.MissionManager");
            if (mmType == null)
            {
                Log.Error<SpectatorJoinFeature>("找不到 Server.Game.MissionManager");
                return;
            }

            object instance = AccessTools.PropertyGetter(mmType, "Instance")?.Invoke(null, null);
            MethodInfo start = AccessTools.Method(mmType, "StartMission", Type.EmptyTypes)
                ?? AccessTools.Method(mmType, "StartMission");
            if (instance == null || start == null)
            {
                Log.Error<SpectatorJoinFeature>("MissionManager.Instance / StartMission 不可用");
                return;
            }

            start.Invoke(instance, start.GetParameters().Length == 0 ? null : new object[start.GetParameters().Length]);
        }

        private static void InvokeAwardStartRound()
        {
            Type t = AccessTools.TypeByName("Server.Game.AwardManager");
            if (t == null)
            {
                Log.Warn<SpectatorJoinFeature>("找不到 Server.Game.AwardManager");
                return;
            }
            object inst = AccessTools.PropertyGetter(t, "Instance")?.Invoke(null, null);
            AccessTools.Method(t, "StartRound")?.Invoke(inst, null);
        }
    }
}
