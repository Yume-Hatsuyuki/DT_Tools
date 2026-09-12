using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Server.Game;
using UnityEngine;
using GamePlayer = Server.Game.Player;

namespace DT_Tools.Patches.System
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   GameRoom::StartDetective()
    ///
    /// <b>原版效果</b>：
    ///   方法开头 Player black = TrialManager.Instance.Black; 之后直接
    ///   if (black.IsAlive) 给黑方发送当前地图包，没有 null 判断。
    ///   正常流程中 Black 一定已由 Corpse.EndSurvival → TrialManager.Init 赋值；
    ///   但无尸体直接进入调查阶段时（控制台 /enter_detective 的单人调试场景），
    ///   Black 为 null，此处抛 NullReferenceException。异常被 JobSerializer 的
    ///   try-catch 吞掉仅记日志，导致 DetectiveTick 不启动、外层
    ///   ChangeGameState 回调里的 IsTransitioning = false 不执行，房间永久卡死。
    ///   同一方法后文（if (black != null) 请求录像带处）反而有 null 判断，
    ///   可确认开头这里是原版漏写。
    ///
    /// <b>修改后效果</b>：
    ///   black.IsAlive 改为 null 安全调用（player != null &amp;&amp; player.IsAlive）。
    ///   正常对局 Black 必非空，行为完全不变；无凶手时安全跳过凶手地图包，
    ///   后续线索分发、S_FADE_IN、DetectiveTick 全部正常执行。
    ///
    /// <b>修改方式</b>：
    ///   Transpiler：StartDetective 方法体内对 Player::get_IsAlive 的 callvirt
    ///   就地改为对 IsAliveNullSafe 的 call（栈行为一致，且保留原指令的 label）。
    ///   方法体内仅此一处 IsAlive 调用；SyncAllPlayer lambda 中的 player2.IsAlive
    ///   位于编译器生成的独立方法，不会被本补丁触及。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "StartDetective",
        "修复原版 GameRoom.StartDetective 在无凶手（Black 为 null）时直接解引用的空引用 bug；\n" +
        "控制台命令 /enter_detective 无尸体进入调查阶段时依赖此补丁，正常对局 Black 必非空故无影响，默认开启。",
        defaultEnabled: true,
        author: "梦初雪")]
    internal static class Patch_StartDetective
    {
        /// <summary>补丁是否已成功应用；控制台命令据此判断能否无凶手裸进调查阶段。</summary>
        public static bool IsApplied { get; private set; }

        [HarmonyPatch(typeof(GameRoom), "StartDetective")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo getterIsAlive =
                AccessTools.PropertyGetter(typeof(GamePlayer), nameof(GamePlayer.IsAlive));
            MethodInfo nullSafeGetIsAlive =
                AccessTools.Method(typeof(Patch_StartDetective), nameof(IsAliveNullSafe));

            int hits = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(getterIsAlive))
                {
                    // 就地替换 opcode/operand，保留指令上已有的 label 与异常块信息
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = nullSafeGetIsAlive;
                    hits++;
                }
            }

            if (hits == 0)
            {
                IsApplied = false;
                Debug.LogError(
                    "[DT_Tools][StartDetective] 未匹配到 get_IsAlive 指令，" +
                    "游戏版本可能已更新，null 安全补丁未生效；/enter_detective 无尸体时将无法进入调查阶段。");
            }
            else
            {
                IsApplied = true;
                Debug.Log($"[DT_Tools][StartDetective] null 安全补丁已应用（替换 {hits} 处 IsAlive 调用）。");
            }

            return codes.AsEnumerable();
        }

        /// <summary>
        /// 替换 black.IsAlive：玩家为 null 时返回 false，
        /// 使无凶手的调查阶段跳过凶手专属地图包而不是抛空引用。
        /// </summary>
        private static bool IsAliveNullSafe(GamePlayer player)
        {
            return player != null && player.IsAlive;
        }
    }
}
