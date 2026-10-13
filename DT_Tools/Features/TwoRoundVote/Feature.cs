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
        Tags = FeatureTag.GameVote | FeatureTag.GameRound,
        Author = "合理")]
    public sealed class TwoRoundVoteFeature
    {
        /// <summary>
        /// 当前对局是否已进入第二轮。匹配开始时（StartPick）复位。
        /// </summary>
        private static bool IsRoundTwo;

        /// <summary>
        /// 新对局开始（Lobby→PickCharacter）时复位回合状态。
        /// </summary>
        [HarmonyPatch(typeof(GameRoom), "StartPick")]
        [HarmonyPrefix]
        private static void PrefixStartPick()
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            IsRoundTwo = false;
        }

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

            ResetMissionProgress(participantCount);
            TryRestoreFirstBlood(room, 0);
            AwardManager.Instance.StartRound();

            // 时间重置：先重置为原版初始值，再预扣 EndState(Trial→Survive) 附加的 +20s，
            // 使第二轮实际剩余时间与首轮初始时间完全一致。
            TimeManager.Instance.ResetStopWatch();
            TimeManager.Instance.ResetSurvival();
            TimeManager.Instance.UpdateRemainTime(-20f);

            IsRoundTwo = true;
            room.ChangeGameState(EGameState.Survive);

            // 修复①：第二轮永久锁门 —— 黑幕在第一轮即将结束时锁门（Door.HandleEvent：
            // StateList[2]=0 + LockDoor(State=2) + 预约 TickDoor 倒计时解锁），而
            // TimeManager.ResetSurvival() 会清空生存任务队列（TickDoor 被取消）、
            // Door 未 override InitDevice（空实现不复位）→ 门保持 State=2 且倒计时不再推进，
            // 造成永久上锁。这里把「锁门计时中断残留」的门（State=2 且已过时间 < 锁定时长）
            // 复位为关闭未锁状态；StartSurvive 正常锁的门（StateList[2]==StateList[1]）不受影响。
            ForceResetLockedDoors();

            // 修复②：第二轮任务提示 —— 客户端 ScannedDeviceSet（本局已扫描设备集合）
            // 原版靠新一局 InitRound/场景重载清空，二轮不重载场景导致残留：
            // 客户端 DeviceBase.SetBubble 会强制把已扫描设备的 Bubble 置 0，
            // 使搬运任务手持物品时找不到目标设备（搬运地点不显示）、调查点不显示。
            // 客户端补丁（PostfixStartState_Client）在二轮进入 Survive 时清空该集合，
            // 这里在状态切换后再广播一次全部设备状态，让客户端重新应用 Bubble/MissionType。
            RebroadcastDeviceStates();

            // 修复：第二轮不刷刀 —— InitDevices() 内的 RefreshArmory 执行时 State 仍为 Trial，
            // SpawnFirstWeapon() 的 State==Survive 校验失败导致武器不生成；
            // 状态切换为 Survive 后再触发一次武器刷新（内部含 WeaponSpawnDelay 的 30s 预约逻辑，
            // 重复预约由 CurrentArmory==null 保护，不会重复刷刀）。
            Server.Game.DeviceManager.Instance.RefreshArmory(isInit: true);
        }

        /// <summary>
        /// 第二轮任务重置。MissionManager 在发行程序集中为 internal，经反射调用。
        /// 修复③：原版 StartMission() 内部用 GameRoom.Instance.AliveCount 发布任务——
        /// 二轮在处刑黑方前调用（AliveCount 仍含黑方）→ 任务数量比第二轮实际人数多 1、
        /// GoalPoint=playerCount*15 偏大 → 任务进度百分比增长低于预期。
        /// 这里直接反射调用 private MissionAllocator(int) 并传入第二轮实际参与人数。
        /// </summary>
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
            // 优先 MissionAllocator(int)：按第二轮实际人数发布任务；拿不到时兜底 StartMission()。
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

        /// <summary>
        /// 修复①：复位「锁门计时中断残留」的门 —— 黑幕锁门（StateList[2]=0 + LockDoor 置 State=2
        /// + TickDoor 预约）被 ResetSurvival 清队列打断后，门保持 State=2 且 StateList[2] 不再推进。
        /// 特征：StateList[0]==2（锁）且 StateList[2] &lt; StateList[1]（倒计时未走满）。
        /// StartSurvive 开局锁的门（StateList[2]==StateList[1]）保持原样，与首轮开局一致。
        /// </summary>
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

        /// <summary>
        /// 修复②配套：第二轮进入 Survive 后重广播全部设备状态，
        /// 客户端在清空 ScannedDeviceSet 后据此重新应用 Bubble/MissionType。
        /// </summary>
        private static void RebroadcastDeviceStates()
        {
            foreach (Server.Game.Device device in Server.Game.DeviceManager.Instance.Objects)
                device.BroadcastState();
        }

        /// <summary>
        /// FirstBloodVictimId 为 private set，走原版 Restore 方法复位。
        /// </summary>
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

        /// <summary>
        /// 客户端进程内「本局进入 Survive 的次数」——原版一局只进一次，第二次即二轮。
        /// </summary>
        private static int EnterSurviveCount;

        /// <summary>
        /// 修复②：客户端在二轮（本局第二次进入 Survive）开始时清空 ScannedDeviceSet。
        /// 原版该集合由新一局 InitRound/场景重载清空；二轮不重载场景导致第一轮扫描过的设备残留，
        /// 客户端 DeviceBase.SetBubble 会强制把已扫描设备 Bubble 置 0 → 搬运地点提示/调查点不显示。
        /// 仅在 mod 开启时生效（房主开启后，房主客户端自动修复；同房其他装 mod 且开启的玩家同样修复）。
        /// </summary>
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

        /// <summary>
        /// 客户端对局清理（每局/场景卸载）：重置进入 Survive 计数（原版每局仅一次）。
        /// </summary>
        [HarmonyPatch(typeof(GameManagerEX), "Clear")]
        [HarmonyPostfix]
        private static void PostfixClear(GameManagerEX __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            EnterSurviveCount = 0;
        }

        /// <summary>
        /// 客户端游戏初始化（重连/重开）：重置进入 Survive 计数。
        /// </summary>
        [HarmonyPatch(typeof(GameManagerEX), "InitGame")]
        [HarmonyPostfix]
        private static void PostfixInitGame(GameManagerEX __instance)
        {
            if (!Engine.EnabledOf(typeof(TwoRoundVoteFeature)))
                return;
            EnterSurviveCount = 0;
        }

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
