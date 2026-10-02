using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.DuplicateCharacter
{
    /// <summary>
    /// 取消确认只回操作者本人（CancelPickCharacter，0.1.16b GameRoom.cs:1777）。
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
            // _pickReady 为私有字段：0.1.16b GameRoom.cs:97
            if (!t.Field("_pickReady").GetValue<bool>())
            {
                // 原版英文文案："CancelPickCharacter ignored - phase not ready yet"
                Log.Info<DuplicateCharacterFeature>(
                    $"忽略取消选角：选角阶段尚未就绪。playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            if (TimeManager.Instance.StopWatch >= 40)
            {
                // 原版英文文案："CancelPickCharacter rejected - countdown already started"
                Log.Info<DuplicateCharacterFeature>(
                    $"拒绝取消选角：倒计时已开始。playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            // 私有字段：_pickPlayers:91 / _pickCharacters:93 / _randomPickPlayers:95（0.1.16b GameRoom.cs）
            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();
            var randomPickPlayers = t.Field("_randomPickPlayers").GetValue<HashSet<int>>();

            int playerId = player.PublicInfo.PlayerId;

            if (randomPickPlayers.Remove(playerId))
            {
                // 原版英文文案："Random pick canceled"
                Log.Info<DuplicateCharacterFeature>($"已取消随机选角 - playerId:{playerId}");
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
                // 原版英文文案："CancelPickCharacter ignored - player has no pick"
                Log.Info<DuplicateCharacterFeature>(
                    $"忽略取消选角：该玩家没有选角记录。playerId:{playerId}");
                return false;
            }

            int characterId = pickCharacters[index];
            pickPlayers.RemoveAt(index);
            pickCharacters.RemoveAt(index);
            player.SkillComponent.DeallocateSkill();
            player.CharacterId = 0;
            __instance.MarkRosterDirty();
            Log.Info<DuplicateCharacterFeature>($"取消选角 - playerId:{playerId}/charaId:{characterId}");
            player.Session?.Send(new S_CANCEL_PICK_CHARACTER
            {
                PlayerId = playerId,
                CharacterId = characterId
            });
            return false;
        }
    }

    /// <summary>
    /// 开局占位 + 全员锁定后统一揭晓（PickCharacterTick，0.1.16b GameRoom.cs:1619）。
    /// 要点：
    /// - Prefix 捕获【进入时】的 StopWatch：原方法在【末尾】才 TimeManager.Tick() 自增，
    ///   Postfix 读到的必是自增后值（进入 0 读到 1、进入 40 读到 41）。
    /// - 进入值 == 0：StartPick（:1592）ResetStopWatch 后直接调用，每轮恰一次；此刻选角
    ///   弹窗已随状态切换同步建好，占位包紧随 S_FADE_IN 按序到达，不会被丢弃。
    /// - 「中途进房/重连错过占位」经 0.1.16b 实证不可能发生，无需补发快照：
    ///   ① 新进房被 HandleEnterPlayer 状态闸门拒绝（:1037，闸门 :1083）；② 选角阶段断线由
    ///   HandlePeerDisconnect（:1341/:1367）整除玩家并强制回落 Lobby（:1360-1362/:1396-1398），
    ///   选角轮整体终止（:1621 非 PickCharacter 直接 return）；③ 重连 HandlePeerRejoin
    ///   （:1457-1475）只复活 dummy 化玩家（RevertDummy :1472，仅发生在非选角阶段），实际走
    ///   进房路径且此时已是 Lobby，下一轮占位自然覆盖；④ 迁移期 SetMigrationPhase（:507-511）
    ///   同被 :1083 拒绝，客户端经迁移握手镜像保持状态。
    /// - 进入值 &gt;= 40：CheckPickAllDone 提前 SetStopWatch(40) 或自然数到 40 强制补选，
    ///   两种路径的下一次调用进入值都是 40，且原版补选体已在本 Postfix 前执行完，
    ///   _pickPlayers 即全部最终分位；41~44 共 5 秒准备期（45 切 Survive），
    ///   用 _revealed 保证只揭晓一次。
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

            Log.Info<DuplicateCharacterFeature>($"统一揭晓选角 - 数量:{pickPlayers.Count}");
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
