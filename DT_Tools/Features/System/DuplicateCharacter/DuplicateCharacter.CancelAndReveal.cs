using System.Collections.Generic;
using System.Linq;
using Data;
using HarmonyLib;
using Protocol;
using Server.Game;
using UnityEngine;
using GamePlayer = Server.Game.Player;
using DT_Tools.Core;

namespace DT_Tools.Features.System
{
    internal static partial class DuplicateCharacterFeature
    {
        // ── GameRoom.CancelPickCharacter：取消确认只回操作者本人 ───────────────
        // 原版取消会全员广播 S_CANCEL_PICK_CHARACTER，对端收到后对被取消角色执行
        // RefreshUI——两人同选 X 时，一人取消会把另一人自己格子的置灰错误地恢复。
        // 改为只回本人后，锁定前其他客户端对选角动作完全无感知。
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.CancelPickCharacter))]
        [HarmonyPrefix]
        private static bool PrefixCancelPickCharacter(GameRoom __instance, GamePlayer player)
        {
            if (!FeatureGate.Enabled(typeof(DuplicateCharacterFeature)))
                return true;

            if (__instance.State != EGameState.PickCharacter)
                return false;

            var t = Traverse.Create(__instance);
            if (!t.Field("_pickReady").GetValue<bool>())
            {
                Debug.Log($"[Pick] CancelPickCharacter ignored - phase not ready yet. playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            if (TimeManager.Instance.StopWatch >= 40)
            {
                Debug.Log($"[Pick] CancelPickCharacter rejected - countdown already started. playerId:{player.PublicInfo.PlayerId}");
                return false;
            }

            var pickPlayers = t.Field("_pickPlayers").GetValue<List<int>>();
            var pickCharacters = t.Field("_pickCharacters").GetValue<List<int>>();
            var randomPickPlayers = t.Field("_randomPickPlayers").GetValue<HashSet<int>>();

            int playerId = player.PublicInfo.PlayerId;

            if (randomPickPlayers.Remove(playerId))
            {
                Debug.Log($"[Pick] Random pick canceled - playerId:{playerId}");
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
                Debug.Log($"[Pick] CancelPickCharacter ignored - player has no pick. playerId:{playerId}");
                return false;
            }

            int characterId = pickCharacters[index];
            pickPlayers.RemoveAt(index);
            pickCharacters.RemoveAt(index);
            player.SkillComponent.DeallocateSkill();
            player.CharacterId = 0;
            __instance.MarkRosterDirty();
            Debug.Log($"[Pick] CancelPickCharacter - playerId:{playerId}/charaId:{characterId}");
            player.Session?.Send(new S_CANCEL_PICK_CHARACTER
            {
                PlayerId = playerId,
                CharacterId = characterId
            });
            return false;
        }

        // ── GameRoom.PickCharacterTick：开局占位 + 全员锁定后统一揭晓 ──────────
        // 关键时序：PickCharacterTick 在方法【末尾】才 TimeManager.Tick() 自增，
        // 因此必须在 Prefix（原版方法体之前）捕获进入时的 StopWatch，Postfix 读到
        // 的已经是自增后的值（进入 0 读到 1、进入 40 读到 41），不能直接在
        // Postfix 里读 TimeManager.StopWatch 判断。
        //
        // 进入值 == 0：StartPick 中 ResetStopWatch 后【直接调用】（非主循环 tick），
        // 必然恰好一次。此刻 SyncAllPlayer 已收齐所有客户端完成回包，客户端选角
        // 弹窗在状态切换时已同步创建（OnEnable 同步构建角色格子与玩家行），占位包
        // 紧随 S_FADE_IN 之后按序到达，不会被 _pickPopup==null 丢弃。
        // 进入值 >= 40：全员选完 CheckPickAllDone 提前 SetStopWatch(40)，或自然
        // 数到 40 强制补选——两种路径的下一次调用进入值都是 40，且原版方法体
        // （强制补选）已在本次 Postfix 前执行完，_pickPlayers 包含全部最终分位。
        // 之后 41~44 共 5 秒准备阶段（进入 45 才切 Survive），用 _revealed 保证
        // 只揭晓一次。
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.PickCharacterTick))]
        [HarmonyPrefix]
        private static void PrefixPickCharacterTick(out int __state)
        {
            if (!FeatureGate.Enabled(typeof(DuplicateCharacterFeature)))
            {
                __state = 0;
                return;
            }

            __state = TimeManager.Instance.StopWatch;
        }

        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.PickCharacterTick))]
        [HarmonyPostfix]
        private static void PostfixPickCharacterTick(GameRoom __instance, int __state)
        {
            if (!FeatureGate.Enabled(typeof(DuplicateCharacterFeature)))
                return;

            if (__instance.State != EGameState.PickCharacter)
                return;

            if (__state == 0)
            {
                // 新一轮：重置揭晓标志，并给每人发"其他所有人都在随机选"的占位
                _revealed = false;
                var players = __instance.Players.ToList();
                foreach (GamePlayer receiver in players)
                {
                    foreach (GamePlayer other in players)
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

            Debug.Log($"[Pick] Reveal all picks - count:{pickPlayers.Count}");
            for (int i = 0; i < pickPlayers.Count && i < pickCharacters.Count; i++)
            {
                __instance.Broadcast(new S_PICK_CHARACTER
                {
                    PlayerId = pickPlayers[i],
                    CharacterId = pickCharacters[i]
                });
            }
        }

        // ── 超时补选 / 进房随机分配：不排除已被占用的角色 ─────────────────────
        // 原版 private static int PickRandomOwnedCharacter(Player player, HashSet<int> taken)
        [HarmonyPatch(typeof(GameRoom), "PickRandomOwnedCharacter")]
        [HarmonyPrefix]
        private static void PrefixRandomOwned(ref HashSet<int> taken)
        {
            if (!FeatureGate.Enabled(typeof(DuplicateCharacterFeature)))
                return;

            taken = EmptyTaken;
        }

        private static void InvokeCheckPickAllDone(GameRoom room)
        {
            AccessTools.Method(typeof(GameRoom), "CheckPickAllDone")?.Invoke(room, null);
        }
    }
}
