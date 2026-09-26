using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.DuplicateCharacter
{
    /// <summary>
    /// 取消确认只回操作者本人（CancelPickCharacter，0.1.15b GameRoom.cs:1777）。
    /// 原版取消会全员广播 S_CANCEL_PICK_CHARACTER，对端收到后对被取消角色执行
    /// RefreshUI——两人同选 X 时，一人取消会把另一人自己格子的置灰错误地恢复。
    /// 改为只回本人后，锁定前其他客户端对选角动作完全无感知。
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.CancelPickCharacter))]
    internal static class DuplicateCharacterCancelPickPatch
    {
        private static bool Prefix(GameRoom __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
                return true;

            if (__instance.State != EGameState.PickCharacter)
                return false;

            var t = Traverse.Create(__instance);
            // _pickReady 为私有字段：0.1.15b GameRoom.cs:97
            if (!t.Field("_pickReady").GetValue<bool>())
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"CancelPickCharacter ignored - phase not ready yet. playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            if (TimeManager.Instance.StopWatch >= 40)
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"CancelPickCharacter rejected - countdown already started. playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            // 私有字段：_pickPlayers:91 / _pickCharacters:93 / _randomPickPlayers:95（0.1.15b GameRoom.cs）
            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();
            var randomPickPlayers = t.Field("_randomPickPlayers").GetValue<HashSet<int>>();

            int playerId = player.PublicInfo.PlayerId;

            if (randomPickPlayers.Remove(playerId))
            {
                Log.Info<DuplicateCharacterFeature>($"Random pick canceled - playerId:{playerId}");
                player.Session?.Send(new S_CANCEL_PICK_CHARACTER
                {
                    PlayerId = playerId,
                    CharacterId = -2
                });
                return false;
            }

            int index = pickPlayers.IndexOf(playerId);
            if (index == -1)
            {
                Log.Info<DuplicateCharacterFeature>(
                    $"CancelPickCharacter ignored - player has no pick. playerId:{playerId}");
                return false;
            }

            int characterId = pickCharacters[index];
            pickPlayers.RemoveAt(index);
            pickCharacters.RemoveAt(index);
            player.SkillComponent.DeallocateSkill();
            player.CharacterId = 0;
            __instance.MarkRosterDirty();
            Log.Info<DuplicateCharacterFeature>($"CancelPickCharacter - playerId:{playerId}/charaId:{characterId}");
            player.Session?.Send(new S_CANCEL_PICK_CHARACTER
            {
                PlayerId = playerId,
                CharacterId = characterId
            });
            return false;
        }
    }

    /// <summary>
    /// 开局占位 + 全员锁定后统一揭晓（PickCharacterTick，0.1.15b GameRoom.cs:1619）。
    /// 关键时序：PickCharacterTick 在方法【末尾】才 TimeManager.Tick() 自增，
    /// 因此必须在 Prefix（原版方法体之前）捕获进入时的 StopWatch，Postfix 读到
    /// 的已经是自增后的值（进入 0 读到 1、进入 40 读到 41），不能直接在
    /// Postfix 里读 TimeManager.StopWatch 判断。
    ///
    /// 进入值 == 0：StartPick（0.1.15b GameRoom.cs:1592）中 ResetStopWatch 后【直接调用】
    /// （非主循环 tick），必然恰好一次。此刻 SyncAllPlayer 已收齐所有客户端完成回包，
    /// 客户端选角弹窗在状态切换时已同步创建（OnEnable 同步构建角色格子与玩家行），
    /// 占位包紧随 S_FADE_IN 之后按序到达，不会被 _pickPopup==null 丢弃。
    ///
    /// 「占位包每轮只发一次，选角中途进房/重连的客户端会错过」经 0.1.15b 实证【不可能发生】，
    /// 无需补发快照：
    /// 1) 新进房：HandleEnterPlayer（GameRoom.cs:1037）状态闸门（:1083）在
    ///    State == PickCharacter（含 IsMigrating/IsTransitioning）时直接回 ErrorRoomBusy 拒绝
    ///    ——选角阶段不存在新进房客户端；
    /// 2) 选角阶段断线：HandlePeerDisconnect 两个重载（GameRoom.cs:1341/:1367）均将玩家
    ///    HandleLeavePlayer 整除（:1351-1353 / :1377+:1389）并把房间状态强制回落 Lobby
    ///    （:1360-1362 / :1396-1398），选角阶段整体终止（PickCharacterTick :1621 对非
    ///    PickCharacter 直接 return）——不存在"继续跑的选角轮"，也就没有可错过的占位；
    /// 3) 重连：HandlePeerRejoin（GameRoom.cs:1457-1475）只复活 dummy 化玩家（RevertDummy
    ///    :1472），而 dummy 化仅发生在非 Lobby/非 PickCharacter 分支——选角阶段断线玩家已被
    ///    移除，重连必然走 HandleEnterPlayer，此时状态已是 Lobby；下一轮 StartPick
    ///    （:1592-1617）经 SyncAllPlayer → ResetStopWatch → PickCharacterTick 进入值 0，
    ///    本补丁 __state==0 分支会对当时的全体玩家重发占位，自然覆盖重连者；
    /// 4) 房主迁移：SetMigrationPhase（:507-511）期间 IsMigrating 同样被 :1083 拒绝进房，
    ///    参与迁移的客户端经迁移握手镜像保持本地选角状态，不依赖占位广播重建。
    /// 进入值 &gt;= 40：全员选完 CheckPickAllDone 提前 SetStopWatch(40)，或自然数到 40
    /// 强制补选——两种路径的下一次调用进入值都是 40，且原版方法体（强制补选）已在
    /// 本次 Postfix 前执行完，_pickPlayers 包含全部最终分位。之后 41~44 共 5 秒准备
    /// 阶段（进入 45 才切 Survive），用 _revealed 保证只揭晓一次。
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.PickCharacterTick))]
    internal static class DuplicateCharacterRevealPatch
    {
        // 本轮揭晓是否已完成（进入值 0 时重置，>=40 揭晓后置位，防止 40~44 五个 tick 重复）
        private static bool _revealed;

        private static void Prefix(out int __state)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
            {
                __state = 0;
                return;
            }

            __state = TimeManager.Instance.StopWatch;
        }

        private static void Postfix(GameRoom __instance, int __state)
        {
            if (!Engine.Enabled<DuplicateCharacterFeature>())
                return;

            if (__instance.State != EGameState.PickCharacter)
                return;

            if (__state == 0)
            {
                // 新一轮：重置揭晓标志，并给每人发"其他所有人都在随机选"的占位
                _revealed = false;
                var players = __instance.Players.ToList();
                foreach (Server.Game.Player receiver in players)
                {
                    foreach (Server.Game.Player other in players)
                    {
                        if (other == receiver)
                            continue;
                        receiver.Session?.Send(new S_PICK_CHARACTER
                        {
                            PlayerId = other.PublicInfo.PlayerId,
                            CharacterId = -2
                        });
                    }
                }
                return;
            }

            if (__state < 40 || _revealed)
                return;

            _revealed = true;
            var t = Traverse.Create(__instance);
            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();

            Log.Info<DuplicateCharacterFeature>($"Reveal all picks - count:{pickPlayers.Count}");
            for (int i = 0; i < pickPlayers.Count && i < pickCharacters.Count; i++)
            {
                __instance.Broadcast(new S_PICK_CHARACTER
                {
                    PlayerId = pickPlayers[i],
                    CharacterId = pickCharacters[i]
                });
            }
        }
    }
}
