using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.MultiArmoryWeapon
{
    internal static class MultiArmoryWeaponLogic
    {
        private static List<Server.Game.Armory> GetArmories(Server.Game.DeviceManager dm)
            => Traverse.Create(dm).Field("_armories").GetValue<List<Server.Game.Armory>>();

        private static int DesiredCount(int total)
        {
            int want = MultiArmoryWeaponFeature.WeaponCount;
            if (want <= 0) want = total;
            return Mathf.Clamp(want, 1, Mathf.Max(1, total));
        }

        internal static bool TryGetAnyBlackCatch(Server.Game.TrialManager trial, out int catchId, out string skipReason)
        {
            catchId = -1;
            skipReason = null;
            Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
            if (room == null)
            {
                skipReason = "房间不存在";
                return false;
            }

            List<Server.Game.TrialManager.Candidate> candidates = Traverse.Create(trial)
                .Field("_candidates")
                .GetValue<List<Server.Game.TrialManager.Candidate>>();
            if (candidates == null || candidates.Count <= 1)
            {
                skipReason = $"候选数不足（{candidates?.Count ?? 0}）";
                return false;
            }

            int topCount = 0;
            foreach (Server.Game.TrialManager.Candidate candidate in candidates)
            {
                if (candidate.Info.VoteCount > topCount)
                    topCount = candidate.Info.VoteCount;
            }
            if (topCount <= 0)
            {
                skipReason = "无人投票";
                return false;
            }

            int topId = -1;
            bool tied = false;
            foreach (Server.Game.TrialManager.Candidate candidate in candidates)
            {
                if (candidate.Info.VoteCount != topCount)
                    continue;
                if (topId >= 0)
                {
                    tied = true;
                    break;
                }
                topId = candidate.Info.PlayerId;
            }
            if (tied)
            {
                skipReason = $"平票（各 {topCount} 票）";
                return false;
            }

            if (trial.Black != null && topId == trial.Black.PublicInfo.PlayerId)
            {
                skipReason = "最高票即本庭绑定凶手（原版判白胜）";
                return false;
            }
            Server.Game.Player masterMind = room.MasterMind;
            if (masterMind != null && topId == masterMind.PublicInfo.PlayerId)
            {
                skipReason = "最高票为主谋（原版 MastermindVoteLoss 语义）";
                return false;
            }

            Server.Game.Player target = room.Players.FirstOrDefault(p => p.PublicInfo.PlayerId == topId);
            if (target == null)
            {
                skipReason = $"最高票 #{topId} 不在玩家表";
                return false;
            }
            if (target.Color != EPlayerColor.Black)
            {
                skipReason = $"最高票 #{topId} 非黑方（{target.Color}）";
                return false;
            }
            if (!target.IsAlive)
            {
                skipReason = $"最高票 #{topId} 为已死黑方";
                return false;
            }

            catchId = topId;
            return true;
        }

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

            var open = armories.Where(a => a != null && a.State == (int)EArmoryState.OpenArmory).ToList();
            if (current != null && !open.Contains(current))
                open.Insert(0, current);

            var closed = armories.Where(a => a != null && !open.Contains(a)).ToList();
            while (open.Count < want && closed.Count > 0)
            {
                int idx = Random.Range(0, closed.Count);
                Server.Game.Armory pick = closed[idx];
                closed.RemoveAt(idx);
                OpenArmory(pick, room);
                open.Add(pick);
            }

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
            armory.RefreshState(EArmoryState.OpenArmory);
            try { armory.StartSelectBlackCount(); }
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

        private static void RearmTransfer(Server.Game.Armory armory, Server.Game.GameRoom room)
        {
            armory.DeviceInfo.StateList[2] = room.WeaponMoveSecond;
            armory.DeviceInfo.StateList[3] = 0;
            armory.BroadcastStateInArea();
            Server.Game.TimeManager.Instance.PushSurvivalJob(1, () =>
                Traverse.Create(armory).Method("TickArmory").GetValue());
        }

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
                RearmTransfer(from, room);
                Log.Info<MultiArmoryWeaponFeature>("无空架可转移，重置本架 CD");
                return;
            }

            Server.Game.Armory to = empty[Random.Range(0, empty.Count)];
            CloseArmory(from, room);
            OpenArmory(to, room);

            Server.Game.Armory current = dm.CurrentArmory;
            if (current == null || current == from)
            {
                Traverse.Create(dm).Property("CurrentArmory").SetValue(to);
                Traverse.Create(dm).Field("_lastArmory").SetValue(to);
                Traverse.Create(dm).Property("ArmoryPos").SetValue(to.DeviceInfo.Pos);
            }
            else
            {
                Log.Debug<MultiArmoryWeaponFeature>(
                    $"转移不推进 CurrentArmory：权威架仍为 #{current.ID}（WeaponTime={current.WeaponTime}），本跳 {from.ID} → {to.ID}");
            }

            Server.Game.Player masterMind = room.MasterMind;
            foreach (Server.Game.Player player in room.Players)
            {
                if (player != masterMind)
                    room.AlertMessage(player, ESystemMessageType.WeaponMoved);
            }

            Log.Info<MultiArmoryWeaponFeature>($"武器转移 {from.ID} → {to.ID}");
        }

        public static void SendExtraWeaponForensics(Server.Game.DeviceManager dm, Server.Game.Player player)
        {
            if (dm == null || player == null)
                return;

            List<Server.Game.Armory> armories = GetArmories(dm);
            if (armories == null || armories.Count == 0)
                return;

            Server.Game.Armory current = dm.CurrentArmory;
            int sent = 0;
            foreach (Server.Game.Armory armory in armories)
            {
                if (armory == null || armory == current)
                    continue;
                if (armory.WeaponTime <= 0)
                    continue;
                armory.SendScanInfo(player);
                sent++;
            }
            if (sent > 0)
                Log.Debug<MultiArmoryWeaponFeature>($"取证补发 {sent} 架已拔刀武器时间（跳过 CurrentArmory #{current?.ID ?? 0}）");
        }
    }
}
