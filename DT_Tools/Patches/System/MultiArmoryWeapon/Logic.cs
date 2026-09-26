using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// 多刀编排：刷刀后开放多架、到期随机转移；同步 CurrentArmory 等权威指针以兼容
    /// 原版取刀断言（SendWeapon 要求 ID == CurrentArmory.ID：0.1.15b Armory.cs:134）与任务箭头。
    /// DeviceManager 私有成员定位（0.1.15b DeviceManager.cs）：_armories:28、_lastArmory:14、
    /// CurrentArmory:42（private set 属性）、ArmoryPos:44（private set 属性）。
    /// </summary>
    internal static class MultiArmoryWeaponLogic
    {
        /// <summary>私有字段 _armories（0.1.15b DeviceManager.cs:28）。</summary>
        private static List<Server.Game.Armory> GetArmories(Server.Game.DeviceManager dm)
            => Traverse.Create(dm).Field("_armories").GetValue<List<Server.Game.Armory>>();

        private static int DesiredCount(int total)
        {
            int want = MultiArmoryWeaponFeature.WeaponCount;
            if (want <= 0) want = total;
            return Mathf.Clamp(want, 1, Mathf.Max(1, total));
        }

        /// <summary>刷刀后：按数量开放多架，并让 CurrentArmory 指向其中之一以兼容原版逻辑。</summary>
        public static void OpenAfterSpawn(Server.Game.DeviceManager dm, bool isInit)
        {
            Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
            if (room == null || room.State != EGameState.Survive)
                return;

            List<Server.Game.Armory> armories = GetArmories(dm);
            if (armories == null || armories.Count == 0)
                return;

            int want = DesiredCount(armories.Count);
            Server.Game.Armory current = dm.CurrentArmory;

            // 已开放的
            var open = armories.Where(a => a != null && a.State == (int)EArmoryState.OpenArmory).ToList();
            if (current != null && !open.Contains(current))
                open.Insert(0, current);

            // 补足到 want
            var closed = armories.Where(a => a != null && !open.Contains(a)).ToList();
            while (open.Count < want && closed.Count > 0)
            {
                int idx = Random.Range(0, closed.Count);
                Server.Game.Armory pick = closed[idx];
                closed.RemoveAt(idx);
                OpenArmory(pick, room);
                open.Add(pick);
            }

            // 超出则随机关掉多余的（保留 current）
            while (open.Count > want)
            {
                var candidates = open.Where(a => a != current).ToList();
                if (candidates.Count == 0) break;
                Server.Game.Armory drop = candidates[Random.Range(0, candidates.Count)];
                open.Remove(drop);
                CloseArmory(drop, room);
            }

            Log.Info<MultiArmoryWeaponFeature>(
                $"刷刀后开放 {open.Count}/{armories.Count}（目标 {want}，isInit={isInit}）");
        }

        private static void OpenArmory(Server.Game.Armory armory, Server.Game.GameRoom room)
        {
            if (armory == null || room == null) return;
            armory.RefreshState(EArmoryState.OpenArmory);          // 0.1.15b Armory.cs:56
            try { armory.StartSelectBlackCount(); }                // 0.1.15b Armory.cs:76
            catch (global::System.Exception ex) { Log.Warn<MultiArmoryWeaponFeature>($"StartSelectBlackCount 失败：{ex.Message}"); }
            Server.Game.Player masterMind = room.MasterMind;
            if (masterMind == null || !masterMind.WeaponPickupLocked)
            {
                room.SendSabotageMission(
                    ESchoolMission.ScWeapon,
                    armory.ID,
                    armory.DeviceInfo.Pos,
                    isAdd: true);
            }
        }

        private static void CloseArmory(Server.Game.Armory armory, Server.Game.GameRoom room)
        {
            if (armory == null || room == null) return;
            room.SendSabotageMission(
                ESchoolMission.ScWeapon,
                armory.ID,
                armory.DeviceInfo.Pos,
                isAdd: false);
            armory.RefreshState(EArmoryState.EmptyArmory);
        }

        /// <summary>
        /// 重置本架转移 CD 并续 TickArmory 逐秒链。不能走 RefreshState(OpenArmory)：
        /// 原版仅在状态变化时才重置 StateList[2]/[3] 并续链（0.1.15b Armory.cs:56-74 的
        /// `if (state2 != (int)state)` 门），而到期转移时本架必已是 OpenArmory，该调用是 no-op。
        /// 这里绕过它直写状态，对齐 Armory.cs:62-67 的 OpenArmory 分支语义；
        /// TickArmory 为私有（0.1.15b Armory.cs:154），经 Traverse 续约下一跳（会再进补丁前缀）。
        /// </summary>
        private static void RearmTransfer(Server.Game.Armory armory, Server.Game.GameRoom room)
        {
            armory.DeviceInfo.StateList[2] = room.WeaponMoveSecond;
            armory.DeviceInfo.StateList[3] = 0;
            armory.BroadcastStateInArea();
            Server.Game.TimeManager.Instance.PushSurvivalJob(1, () =>
                Traverse.Create(armory).Method("TickArmory").GetValue());
        }

        /// <summary>到期转移：随机选一个空架，关闭本架、打开目标架。</summary>
        public static void TransferWeaponRandom(
            Server.Game.Armory from, Server.Game.DeviceManager dm, Server.Game.GameRoom room)
        {
            List<Server.Game.Armory> armories = GetArmories(dm);
            if (armories == null || armories.Count < 2)
            {
                RearmTransfer(from, room);
                Log.Info<MultiArmoryWeaponFeature>("仅一架，重置转移 CD");
                return;
            }

            var empty = armories
                .Where(a => a != null && a != from && a.State != (int)EArmoryState.OpenArmory)
                .ToList();

            if (empty.Count == 0)
            {
                // 全满：随机与另一架交换意义不大，重置本架 CD，下一轮再试转移
                RearmTransfer(from, room);
                Log.Info<MultiArmoryWeaponFeature>("无空架可转移，重置本架 CD");
                return;
            }

            Server.Game.Armory to = empty[Random.Range(0, empty.Count)];
            CloseArmory(from, room);
            OpenArmory(to, room);

            // 同步 CurrentArmory / _lastArmory / ArmoryPos，兼容原版取刀与任务箭头
            Traverse.Create(dm).Property("CurrentArmory").SetValue(to);
            Traverse.Create(dm).Field("_lastArmory").SetValue(to);
            Traverse.Create(dm).Property("ArmoryPos").SetValue(to.DeviceInfo.Pos);

            Server.Game.Player masterMind = room.MasterMind;
            foreach (Server.Game.Player player in room.Players)
            {
                if (player != masterMind)
                    room.AlertMessage(player, ESystemMessageType.WeaponMoved);
            }

            Log.Info<MultiArmoryWeaponFeature>($"武器转移 {from.ID} → {to.ID}");
        }
    }
}
