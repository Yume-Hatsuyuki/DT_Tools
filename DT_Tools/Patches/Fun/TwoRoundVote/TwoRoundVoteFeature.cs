using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Protocol;
using Server.Game;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using GamePlayer = Server.Game.Player;

namespace DT_Tools.Patches.Fun.TwoRoundVote
{
    /// <summary>
    /// 二轮投票制（房主权威）：把原版「一次审判即终局」改为「至多两次审判」。
    /// 第一轮投出黑方 → 单独处刑黑方（黑幕不连带处刑），其余白方与黑幕进入第二轮：
    /// 黑幕身份不变、出生点随机重排、任务进度与时间重置，其余规则与原版一轮投票制完全一致。
    /// 第一轮投出黑幕或白方（含平票/弃票）→ 直接结束，结算与原版一致。
    /// 第二轮结算走原版逻辑：黑方胜利时，第一轮已处刑的黑方因 Color=Black 自动计入胜者。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "二轮投票制：第一轮投出黑方后不结束，单独处刑黑方，白方与黑幕进入第二轮（黑幕身份不变、出生点随机、任务与时间重置）；投出黑幕或白方则直接结束。第二轮黑方胜利时，第一轮的黑方同样计入胜利。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "合理")]
    public sealed class TwoRoundVoteFeature
    {
        /// <summary>当前对局是否已进入第二轮。匹配开始时（StartPick）复位。</summary>
        private static bool IsRoundTwo;

        // ---------- 匹配生命周期 ----------

        /// <summary>新对局开始（Lobby→PickCharacter）时复位回合状态。</summary>
        [HarmonyPatch(typeof(GameRoom), "StartPick")]
        [HarmonyPrefix]
        private static void PrefixStartPick()
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            IsRoundTwo = false;
        }

        // ---------- 第一轮：结果包改为「单独处刑黑方」 ----------

        /// <summary>
        /// 原版在黑方被投且黑幕存活时下发 RemainingExecute=2（黑幕连带处刑演出）。
        /// 第一轮必须单独处刑黑方（RemainingExecute=0），黑幕存活进入第二轮；其余字段与奖励逻辑保持原版。
        /// Priority.First：与上游 MultiArmoryWeapon（投票任意黑方胜利，同样整替本方法）并存时，
        /// 二轮制作为更上层的流程改写优先接管第一轮结算演出；MultiArmoryWeapon 在第二轮（走原版结算）正常生效。
        /// </summary>
        [HarmonyPatch(typeof(TrialManager), "StartTrialResult")]
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        private static bool PrefixStartTrialResult(TrialManager __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return true;
            if (IsRoundTwo)
                return true; // 第二轮走原版结算（含黑幕连带处刑演出）

            GameRoom room = GameRoom.Instance;
            Traverse t = Traverse.Create(__instance);
            int mostVoted = t.Method("MostVotedPlayerId").GetValue<int>();
            int blackId = __instance.Black?.PublicInfo.PlayerId ?? 0;
            int mastermindId = room.MasterMind?.PublicInfo.PlayerId ?? 0;

            // 与原版一致的奖励结算
            foreach (TrialManager.Candidate candidate in t.Field("_candidates").GetValue<List<TrialManager.Candidate>>())
            {
                if (blackId != 0 && candidate.VotedTargetId == blackId)
                    AwardManager.Instance.OnVotedBlack(candidate.Info.PlayerId);
            }
            if (room.MasterMind != null && mostVoted == mastermindId)
                AwardManager.Instance.OnMastermindVoted(mastermindId);

            S_RESULT_TRIAL packet = new S_RESULT_TRIAL
            {
                CatchId = mostVoted,
                BlackId = blackId,
                MastermindId = mastermindId,
                RemainingExecute = 0, // 第一轮仅处刑黑方
                RemainingId = 0,
                MastermindExecuted = t.Method("IsMastermindVotedOut", mostVoted).GetValue<bool>()
            };
            room.BroadcastPacketAndWaitResponse(packet,
                (Action)Delegate.CreateDelegate(typeof(Action), __instance,
                    AccessTools.Method(typeof(TrialManager), "FinalizeTrialResult")));
            return false;
        }

        // ---------- 第一轮：投出黑方 → 进入第二轮；否则原版结束 ----------

        /// <summary>
        /// 原版：最高票为黑方 → WhiteWin 并结束；否则 BlackWin 并结束。
        /// 二轮制：第一轮最高票为黑方 → 单独处刑并全量重置后进入第二轮（Trial→Survive）；
        /// 第一轮最高票为黑幕/白方（或平票/弃票）→ 放行原版直接结束。
        /// Priority.First：与 MultiArmoryWeapon（任意黑方胜利判白胜）并存时，第一轮投出黑方
        /// 优先进入第二轮而非直接判胜；MultiArmoryWeapon 未命中时放行原版，二轮局第二轮亦正常生效。
        /// </summary>
        [HarmonyPatch(typeof(TrialManager), "FinalizeTrialResult")]
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        private static bool PrefixFinalizeTrialResult(TrialManager __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return true;
            if (IsRoundTwo)
                return true; // 第二轮按原版结束对局（无论黑方是否被投出）

            int mostVoted = Traverse.Create(__instance).Method("MostVotedPlayerId").GetValue<int>();
            int blackId = __instance.Black?.PublicInfo.PlayerId ?? 0;
            if (blackId == 0 || mostVoted != blackId)
                return true; // 未投出黑方（黑幕/白方被投、平票、弃票）→ 原版直接结束

            StartSecondRound();
            return false;
        }

        /// <summary>
        /// 第二轮开始：处刑第一轮黑方（交由 EndState(Trial→Survive) 的 EndTrial 执行），
        /// 全量重置回合状态（镜像 GameRoom.GameStart，仅幸存者分配出生点），
        /// 时间重置（预扣 EndState 的 +20s 补偿），黑幕身份不变（不重跑 StartPick）。
        /// </summary>
        private static void StartSecondRound()
        {
            GameRoom room = GameRoom.Instance;
            GamePlayer executedBlack = TrialManager.Instance.Black;

            // EndTrial() 仅在 ResultType==WhiteWin 时处刑 Trial.Black —— 保持 WhiteWin 让原路径处刑黑方
            room.ResultType = EResultType.WhiteWin;

            // 全新回合状态（对齐 GameRoom.GameStart，但只给幸存者分配出生点）
            // 客户端全局也有 ItemManager / DeviceManager，必须用 Server.Game 全名
            Server.Game.ItemManager.Instance.ClearWithDespawn();
            room.CorpseMetas.Clear();
            int participantCount = room.AlivePlayers.Count - (executedBlack != null && executedBlack.IsAlive ? 1 : 0);
            room.RoundStartPlayerCount = Math.Max(0, participantCount);
            foreach (GamePlayer p in room.Players)
                p.Clear();

            List<PosInfo> spawns = Managers.Data.MapData.StartPosList != null
                ? Managers.Data.MapData.StartPosList.ToList()
                : new List<PosInfo>();
            if (spawns.Count == 0)
            {
                PosInfo fallback = Managers.Data.MapData.ErrorPos ?? Managers.Data.MapData.LobbyPos;
                if (fallback == null)
                    fallback = new PosInfo { X = 0f, Y = 0f };
                foreach (GamePlayer p in room.AlivePlayers)
                {
                    if (p == executedBlack)
                        continue;
                    p.GameStart(fallback);
                }
            }
            else
            {
                spawns = Util.Shuffle(spawns, spawns.Count);
                int idx = 0;
                foreach (GamePlayer p in room.AlivePlayers)
                {
                    if (p == executedBlack)
                        continue;
                    p.GameStart(spawns[idx % spawns.Count]);
                    idx++;
                }
            }

            // 修复：第二轮不重建场景，设备实例沿用第一轮（Alchemist/Drink/Miner/Mission
            // 家族等任务道具设备）——逐台复位本轮状态（StateList/MissionType/Bubble/使用标记），
            // 否则第一轮遗留的任务道具状态会阻塞第二轮交互。
            foreach (Server.Game.Device device in Server.Game.DeviceManager.Instance.Objects)
            {
                device.InitDevice();
            }

            Server.Game.DeviceManager.Instance.InitDevices();
            Server.Game.DeviceManager.Instance.InitStorage();

            ResetMissionProgress();
            TryRestoreFirstBlood(room, 0);
            AwardManager.Instance.StartRound();

            // 时间重置：先重置为原版初始值，再预扣 EndState(Trial→Survive) 附加的 +20s，
            // 使第二轮实际剩余时间与首轮初始时间完全一致。
            TimeManager.Instance.ResetStopWatch();
            TimeManager.Instance.ResetSurvival();
            TimeManager.Instance.UpdateRemainTime(-20f);

            IsRoundTwo = true;
            room.ChangeGameState(EGameState.Survive);

            // 修复：第二轮不刷刀 —— InitDevices() 内的 RefreshArmory 执行时 State 仍为 Trial，
            // SpawnFirstWeapon() 的 State==Survive 校验失败导致武器不生成；
            // 状态切换为 Survive 后再触发一次武器刷新（内部含 WeaponSpawnDelay 的 30s 预约逻辑，
            // 重复预约由 CurrentArmory==null 保护，不会重复刷刀）。
            Server.Game.DeviceManager.Instance.RefreshArmory(isInit: true);
        }

        /// <summary>MissionManager 在发行程序集中为 internal，经反射调用 StartMission 重置任务进度。</summary>
        private static void ResetMissionProgress()
        {
            Type mmType = AccessTools.TypeByName("Server.Game.MissionManager");
            if (mmType == null)
            {
                Log.Error<TwoRoundVoteFeature>("找不到 Server.Game.MissionManager");
                return;
            }
            object instance = AccessTools.PropertyGetter(mmType, "Instance")?.Invoke(null, null);
            MethodInfo start = AccessTools.Method(mmType, "StartMission", Type.EmptyTypes)
                ?? AccessTools.Method(mmType, "StartMission");
            if (instance == null || start == null)
            {
                Log.Error<TwoRoundVoteFeature>("MissionManager.Instance / StartMission 不可用");
                return;
            }
            start.Invoke(instance, start.GetParameters().Length == 0 ? null : new object[start.GetParameters().Length]);
        }

        /// <summary>FirstBloodVictimId 为 private set，走原版 Restore 方法复位。</summary>
        private static void TryRestoreFirstBlood(GameRoom room, int victimId)
        {
            MethodInfo m = AccessTools.Method(typeof(GameRoom), "RestoreFirstBloodVictimId");
            if (m == null)
            {
                Traverse.Create(room).Property(nameof(room.FirstBloodVictimId)).SetValue(victimId);
                return;
            }
            m.Invoke(room, new object[] { victimId });
        }

        // ---------- 第二轮：清掉第一轮遗留的审判状态 ----------

        /// <summary>
        /// 第二轮 Survive 开始时清空 TrialManager 残留（Corpse/Black/候选等），
        /// 避免下一场审判 Init 前的旧尸体引用影响 GetDisplayTime 等判断。
        /// </summary>
        [HarmonyPatch(typeof(GameRoom), "StartState")]
        [HarmonyPostfix]
        private static void PostfixStartState(EGameState state)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            if (IsRoundTwo && state == EGameState.Survive)
                TrialManager.Instance.Clear();
        }
    }
}
