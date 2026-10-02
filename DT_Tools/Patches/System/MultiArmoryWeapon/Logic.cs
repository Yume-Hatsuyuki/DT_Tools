using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.System.MultiArmoryWeapon
{
    /// <summary>
    /// 多刀编排：刷刀后开放多架、到期随机转移；同步 CurrentArmory 等权威指针以兼容
    /// 原版取刀断言（SendWeapon 要求 ID == CurrentArmory.ID：0.1.16a Server.Game/Armory.cs:134）与任务箭头。
    /// DeviceManager 私有成员定位（0.1.16a Server.Game/DeviceManager.cs）：_armories:28、_lastArmory:14、
    /// CurrentArmory:42（private set 属性）、ArmoryPos:44（private set 属性）。
    /// </summary>
    internal static class MultiArmoryWeaponLogic
    {
        /// <summary>私有字段 _armories（0.1.16a Server.Game/DeviceManager.cs:28）。</summary>
        private static List<Server.Game.Armory> GetArmories(Server.Game.DeviceManager dm)
            => Traverse.Create(dm).Field("_armories").GetValue<List<Server.Game.Armory>>();

        private static int DesiredCount(int total)
        {
            int want = MultiArmoryWeaponFeature.WeaponCount;
            if (want <= 0) want = total;
            return Mathf.Clamp(want, 1, Mathf.Max(1, total));
        }

        /// <summary>
        /// 「投票任意黑方胜利」命中判定：庭审最高票（唯一）是任意活着的黑方（非本庭绑定凶手、非主谋）时为 true。
        /// 不能复用原版 MostVotedPlayerId 当「最高票玩家」：它的返回值语义是「原版处决目标」——
        /// 只有最高票为绑定凶手（TrialManager.cs:856-859）或主谋（:861-864）才返回对应 id，
        /// 其余情形（含投中其他黑方与白方）一律返回 -1（:866），据此过滤会永远不命中。
        /// 因此这里自读 _candidates（0.1.16a Server.Game/TrialManager.cs:47，私有）统计唯一最高票：
        /// 无票（:835-838）、平票（:840-853）、绑定凶手（原版已判白胜 :787-791）、主谋（独立出局语义 :801-816）
        /// 一律交还原版；其余仅当目标是活着的黑方才接管——死后被票出不算（原版 Trial Survive 豁免也仅限绑定凶手）。
        /// </summary>
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
            armory.RefreshState(EArmoryState.OpenArmory);          // 0.1.16a Server.Game/Armory.cs:56
            try { armory.StartSelectBlackCount(); }                // 0.1.16a Server.Game/Armory.cs:76
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
        /// 原版仅在状态变化时才重置 StateList[2]/[3] 并续链（0.1.16a Server.Game/Armory.cs:56-74 的
        /// `if (state2 != (int)state)` 门），而到期转移时本架必已是 OpenArmory，该调用是 no-op。
        /// 这里绕过它直写状态，对齐 Server.Game/Armory.cs:62-67 的 OpenArmory 分支语义；
        /// TickArmory 为私有（0.1.16a Server.Game/Armory.cs:154），经 Traverse 续约下一跳（会再进补丁前缀）。
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
