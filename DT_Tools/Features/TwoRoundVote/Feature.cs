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

namespace DT_Tools.Features.TwoRoundVote
{
    [HarmonyPatch]
    [PatchFeature(
        "二轮投票制：第一轮投出黑方后不结束，单独处刑黑方，白方与黑幕进入第二轮（黑幕身份不变、出生点随机、任务与时间重置）；投出黑幕或白方则直接结束。第二轮黑方胜利时，第一轮的黑方同样计入胜利。房主侧生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameVote | FeatureTag.GameRound,
        Author = "合理")]
    public sealed class TwoRoundVoteFeature
    {
        private static bool IsRoundTwo;

        [HarmonyPatch(typeof(GameRoom), "StartPick")]
        [HarmonyPrefix]
        private static void PrefixStartPick()
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            IsRoundTwo = false;
        }

        [HarmonyPatch(typeof(TrialManager), "StartTrialResult")]
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        private static bool PrefixStartTrialResult(TrialManager __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return true;
            if (IsRoundTwo)
                return true;

            GameRoom room = GameRoom.Instance;
            Traverse t = Traverse.Create(__instance);
            int mostVoted = t.Method("MostVotedPlayerId").GetValue<int>();
            int blackId = __instance.Black?.PublicInfo.PlayerId ?? 0;
            int mastermindId = room.MasterMind?.PublicInfo.PlayerId ?? 0;

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
                RemainingExecute = 0,
                RemainingId = 0,
                MastermindExecuted = t.Method("IsMastermindVotedOut", mostVoted).GetValue<bool>()
            };
            room.BroadcastPacketAndWaitResponse(packet,
                (Action)Delegate.CreateDelegate(typeof(Action), __instance,
                    AccessTools.Method(typeof(TrialManager), "FinalizeTrialResult")));
            return false;
        }

        [HarmonyPatch(typeof(TrialManager), "FinalizeTrialResult")]
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        private static bool PrefixFinalizeTrialResult(TrialManager __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return true;
            if (IsRoundTwo)
                return true;

            int mostVoted = Traverse.Create(__instance).Method("MostVotedPlayerId").GetValue<int>();
            int blackId = __instance.Black?.PublicInfo.PlayerId ?? 0;
            if (blackId == 0 || mostVoted != blackId)
                return true;

            StartSecondRound();
            return false;
        }

        private static void StartSecondRound()
        {
            GameRoom room = GameRoom.Instance;
            GamePlayer executedBlack = TrialManager.Instance.Black;

            room.ResultType = EResultType.WhiteWin;

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

            foreach (Server.Game.Device device in Server.Game.DeviceManager.Instance.Objects)
            {
                device.InitDevice();
            }

            Server.Game.DeviceManager.Instance.InitDevices();
            Server.Game.DeviceManager.Instance.InitStorage();

            ResetMissionProgress(participantCount);
            TryRestoreFirstBlood(room, 0);
            AwardManager.Instance.StartRound();

            TimeManager.Instance.ResetStopWatch();
            TimeManager.Instance.ResetSurvival();
            TimeManager.Instance.UpdateRemainTime(-20f);

            IsRoundTwo = true;
            room.ChangeGameState(EGameState.Survive);

            ForceResetLockedDoors();

            RebroadcastDeviceStates();

            Server.Game.DeviceManager.Instance.RefreshArmory(isInit: true);
        }

        private static void ResetMissionProgress(int playerCount)
        {
            Type mmType = AccessTools.TypeByName("Server.Game.MissionManager");
            if (mmType == null)
            {
                Log.Error<TwoRoundVoteFeature>("找不到 Server.Game.MissionManager");
                return;
            }
            object instance = AccessTools.PropertyGetter(mmType, "Instance")?.Invoke(null, null);
            if (instance == null)
            {
                Log.Error<TwoRoundVoteFeature>("MissionManager.Instance 不可用");
                return;
            }
            MethodInfo allocator = AccessTools.Method(mmType, "MissionAllocator", new[] { typeof(int) })
                ?? AccessTools.Method(mmType, "MissionAllocator");
            if (allocator != null && allocator.GetParameters().Length == 1)
            {
                allocator.Invoke(instance, new object[] { Math.Max(playerCount, 1) });
                return;
            }
            MethodInfo start = AccessTools.Method(mmType, "StartMission", Type.EmptyTypes)
                ?? AccessTools.Method(mmType, "StartMission");
            if (start == null)
            {
                Log.Error<TwoRoundVoteFeature>("MissionManager.MissionAllocator / StartMission 不可用");
                return;
            }
            start.Invoke(instance, start.GetParameters().Length == 0 ? null : new object[start.GetParameters().Length]);
        }

        private static void ForceResetLockedDoors()
        {
            foreach (Server.Game.Device device in Server.Game.DeviceManager.Instance.Objects)
            {
                if (device is Server.Game.Door door &&
                    door.DeviceInfo.StateList.Count >= 3 &&
                    door.DeviceInfo.StateList[0] == 2 &&
                    door.DeviceInfo.StateList[2] < door.DeviceInfo.StateList[1])
                {
                    door.DeviceInfo.StateList[0] = 1;
                    door.DeviceInfo.StateList[2] = door.DeviceInfo.StateList[1];
                    door.BroadcastState();
                    Log.Info<TwoRoundVoteFeature>($"二轮锁门复位 door={door.ID}");
                }
            }
        }

        private static void RebroadcastDeviceStates()
        {
            foreach (Server.Game.Device device in Server.Game.DeviceManager.Instance.Objects)
                device.BroadcastState();
        }

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

        private static int EnterSurviveCount;

        [HarmonyPatch(typeof(GameManagerEX), "StartState")]
        [HarmonyPostfix]
        private static void PostfixStartState_Client(GameManagerEX __instance, EGameState state)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            if (state != EGameState.Survive)
                return;
            EnterSurviveCount++;
            if (EnterSurviveCount >= 2)
                __instance.ScannedDeviceSet.Clear();
        }

        [HarmonyPatch(typeof(GameManagerEX), "Clear")]
        [HarmonyPostfix]
        private static void PostfixClear(GameManagerEX __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            EnterSurviveCount = 0;
        }

        [HarmonyPatch(typeof(GameManagerEX), "InitGame")]
        [HarmonyPostfix]
        private static void PostfixInitGame(GameManagerEX __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            EnterSurviveCount = 0;
        }

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
