using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using Server.Game;
using UnityEngine;
using DT_Tools.Core;
using GameArmory = Server.Game.Armory;
using GameDeviceManager = Server.Game.DeviceManager;
using GamePlayer = Server.Game.Player;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// 多武器架同时有刀，且每把刀独立倒计时、到期后随机转移到空架。
    /// 原版仅 CurrentArmory 会 Tick 转移 CD。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "MultiArmoryWeapon",
        description: "武器架多刀：同时开放多处武器架，每把刀独立转移 CD，到期随机换架。WeaponCount=0 表示全部架。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class MultiArmoryWeaponFeature
    {
        [ConfigField(0, "同时持有武器的架数。0=全部；超过地图架数时按架数封顶。")]
        public static ConfigEntry<int> WeaponCount;

        private static List<GameArmory> GetArmories(GameDeviceManager dm)
        {
            return Traverse.Create(dm).Field("_armories").GetValue<List<GameArmory>>();
        }

        private static int DesiredCount(int total)
        {
            int want = WeaponCount != null ? WeaponCount.Value : 0;
            if (want <= 0) want = total;
            return Mathf.Clamp(want, 1, Mathf.Max(1, total));
        }

        private static void OpenArmory(GameArmory armory, GameRoom room)
        {
            if (armory == null || room == null) return;
            armory.RefreshState(EArmoryState.OpenArmory);
            try { armory.StartSelectBlackCount(); } catch { /* */ }
            GamePlayer masterMind = room.MasterMind;
            if (masterMind == null || !masterMind.WeaponPickupLocked)
            {
                room.SendSabotageMission(
                    ESchoolMission.ScWeapon,
                    armory.ID,
                    armory.DeviceInfo.Pos,
                    isAdd: true);
            }
        }

        private static void CloseArmory(GameArmory armory, GameRoom room)
        {
            if (armory == null || room == null) return;
            room.SendSabotageMission(
                ESchoolMission.ScWeapon,
                armory.ID,
                armory.DeviceInfo.Pos,
                isAdd: false);
            armory.RefreshState(EArmoryState.EmptyArmory);
        }

        /// <summary>刷刀后：按数量开放多架，并让 CurrentArmory 指向其中之一以兼容原版逻辑。</summary>
        [HarmonyPatch(typeof(GameDeviceManager), nameof(GameDeviceManager.SpawnNextWeapon))]
        [HarmonyPostfix]
        private static void PostfixSpawnNextWeapon(GameDeviceManager __instance, bool isInit)
        {
            if (!FeatureGate.Enabled(typeof(MultiArmoryWeaponFeature)))
                return;

            var room = GameRoom.Instance;
            if (room == null || room.State != EGameState.Survive)
                return;

            var armories = GetArmories(__instance);
            if (armories == null || armories.Count == 0)
                return;

            int want = DesiredCount(armories.Count);
            GameArmory current = __instance.CurrentArmory;

            // 已开放的
            var open = armories.Where(a => a != null && a.State == (int)EArmoryState.OpenArmory).ToList();
            if (current != null && !open.Contains(current))
                open.Insert(0, current);

            // 补足到 want
            var closed = armories.Where(a => a != null && !open.Contains(a)).ToList();
            while (open.Count < want && closed.Count > 0)
            {
                int idx = Random.Range(0, closed.Count);
                var pick = closed[idx];
                closed.RemoveAt(idx);
                OpenArmory(pick, room);
                open.Add(pick);
            }

            // 超出则随机关掉多余的（保留 current）
            while (open.Count > want)
            {
                var candidates = open.Where(a => a != current).ToList();
                if (candidates.Count == 0) break;
                var drop = candidates[Random.Range(0, candidates.Count)];
                open.Remove(drop);
                CloseArmory(drop, room);
            }

            FeatureLogRegistry.Info("MultiArmoryWeapon",
                $"刷刀后开放 {open.Count}/{armories.Count}（目标 {want}，isInit={isInit}）");
        }

        /// <summary>
        /// 原版 Tick 只服务 CurrentArmory。开启功能后：任意 Open 架都自己走 CD，
        /// 到期后把刀随机挪到空架（关闭本架、打开目标架）。
        /// </summary>
        [HarmonyPatch(typeof(GameArmory), "TickArmory")]
        [HarmonyPrefix]
        private static bool PrefixTickArmory(GameArmory __instance)
        {
            if (!FeatureGate.Enabled(typeof(MultiArmoryWeaponFeature)))
                return true;

            var room = GameRoom.Instance;
            var dm = GameDeviceManager.Instance;
            if (room == null || dm == null || room.State != EGameState.Survive)
                return false;

            if (__instance.State != (int)EArmoryState.OpenArmory)
                return false;

            var states = __instance.DeviceInfo?.StateList;
            if (states == null || states.Count < 4)
                return false;

            // StateList[2]=总秒数, [3]=已过秒数
            if (states[2] > states[3])
            {
                states[3]++;
                if (states[2] <= states[3])
                {
                    TransferWeaponRandom(__instance, dm, room);
                }
                else
                {
                    try { Traverse.Create(__instance).Method("BroadcastStateInArea").GetValue(); }
                    catch { /* */ }
                    TimeManager.Instance.PushSurvivalJob(1, () =>
                    {
                        Traverse.Create(__instance).Method("TickArmory").GetValue();
                    });
                }
            }
            return false;
        }

        private static void TransferWeaponRandom(GameArmory from, GameDeviceManager dm, GameRoom room)
        {
            var armories = GetArmories(dm);
            if (armories == null || armories.Count < 2)
            {
                // 只有一架：重置 CD
                from.RefreshState(EArmoryState.OpenArmory);
                FeatureLogRegistry.Info("MultiArmoryWeapon", "仅一架，重置转移 CD");
                return;
            }

            var empty = armories
                .Where(a => a != null && a != from && a.State != (int)EArmoryState.OpenArmory)
                .ToList();

            if (empty.Count == 0)
            {
                // 全满：随机与另一架交换意义不大，重置本架 CD
                from.RefreshState(EArmoryState.OpenArmory);
                FeatureLogRegistry.Info("MultiArmoryWeapon", "无空架可转移，重置本架 CD");
                return;
            }

            var to = empty[Random.Range(0, empty.Count)];
            CloseArmory(from, room);
            OpenArmory(to, room);

            // 同步 CurrentArmory，兼容取刀/任务箭头
            Traverse.Create(dm).Property("CurrentArmory").SetValue(to);
            Traverse.Create(dm).Field("_lastArmory").SetValue(to);
            Traverse.Create(dm).Property("ArmoryPos").SetValue(to.DeviceInfo.Pos);

            GamePlayer masterMind = room.MasterMind;
            foreach (GamePlayer player in room.Players)
            {
                if (player != masterMind)
                    room.AlertMessage(player, ESystemMessageType.WeaponMoved);
            }

            FeatureLogRegistry.Info("MultiArmoryWeapon",
                $"武器转移 {from.ID} → {to.ID}");
        }
    }
}
