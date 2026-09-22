using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data;
using HarmonyLib;
using Protocol;
using Server.Game;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    internal static partial class LobbyMaxPlayersFeature
    {
        /// <summary>
        /// GameStart：StartPosList 打乱后分配。
        /// 人数不超过出生点时与原版一致；超出则 i % Count 循环复用，避免越界。
        /// 仅当 MaxMembers > 8 时接管，否则走原版。
        /// </summary>
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.GameStart))]
        [HarmonyPrefix]
        private static bool PrefixGameStart(GameRoom __instance)
        {
            if (!FeatureGate.Enabled(typeof(LobbyMaxPlayersFeature)))
                return true;

            if (MaxMembers <= 8)
                return true;

            // ItemManager 是 public；DeviceManager / MissionManager / AwardManager 需全名或反射
            Server.Game.ItemManager.Instance.ClearWithDespawn();

            __instance.CorpseMetas.Clear();
            __instance.RoundStartPlayerCount = __instance.Players.Count;
            Debug.Log(
                $"[Kill] 라운드 시작 인원 {__instance.RoundStartPlayerCount}명 → 블랙 살인 한도 {__instance.BlackKillLimit}회 (LobbyMaxPlayers cyclic spawn)");

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
                Debug.LogWarning("[LobbyMaxPlayers] StartPosList empty, using ErrorPos/LobbyPos fallback");
                foreach (GamePlayer player in __instance.Players)
                    player.GameStart(fallback);
            }
            else
            {
                list = Util.Shuffle(list, list.Count);
                int n = __instance.Players.Count;
                for (int i = 0; i < n; i++)
                    __instance.Players[i].GameStart(list[i % list.Count]);

                if (n > list.Count)
                    Debug.Log($"[LobbyMaxPlayers] Players={n} > StartPos={list.Count}, cyclic reuse");
            }

            // 客户端全局也有 DeviceManager，必须用 Server.Game 全名
            Server.Game.DeviceManager.Instance.InitDevices();
            Server.Game.DeviceManager.Instance.InitStorage();

            InvokeMissionStart();

            // FirstBloodVictimId 只有 private set，用原版 Restore 或 Traverse
            if (!TryRestoreFirstBlood(__instance, 0))
                Traverse.Create(__instance).Property("FirstBloodVictimId").SetValue(0);

            InvokeAwardStartRound();

            return false;
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
            // MissionManager 在发行程序集中为 internal
            Type mmType = AccessTools.TypeByName("Server.Game.MissionManager");
            if (mmType == null)
            {
                Debug.LogError("[LobbyMaxPlayers] 找不到 Server.Game.MissionManager");
                return;
            }

            object instance = AccessTools.PropertyGetter(mmType, "Instance")?.Invoke(null, null);
            MethodInfo start = AccessTools.Method(mmType, "StartMission", Type.EmptyTypes)
                ?? AccessTools.Method(mmType, "StartMission");
            if (instance == null || start == null)
            {
                Debug.LogError("[LobbyMaxPlayers] MissionManager.Instance / StartMission 不可用");
                return;
            }

            start.Invoke(instance, start.GetParameters().Length == 0 ? null : new object[start.GetParameters().Length]);
        }

        private static void InvokeAwardStartRound()
        {
            Type amType = AccessTools.TypeByName("Server.Game.AwardManager")
                ?? typeof(GameRoom).Assembly.GetType("Server.Game.AwardManager");
            // 若 public，直接试
            try
            {
                Type t = AccessTools.TypeByName("Server.Game.AwardManager");
                if (t != null)
                {
                    object inst = AccessTools.PropertyGetter(t, "Instance")?.Invoke(null, null);
                    AccessTools.Method(t, "StartRound")?.Invoke(inst, null);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyMaxPlayers] AwardManager.StartRound 反射失败: {ex.Message}");
            }
        }
    }
}
