using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Server.Game;
using UnityEngine;
using DT_Tools.Core;
using GamePlayer = Server.Game.Player;

namespace DT_Tools.Features.System
{
    /// <summary>
    /// StartDetective 开头 black.IsAlive 在 Black==null 时 NRE。将 get_IsAlive 换为 null 安全调用。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "StartDetective",
        description: "调查阶段补丁：修复无凶手时 StartDetective 空引用；/enter_detective 依赖此补丁。",
        defaultEnabled: true,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class DetectivePhaseFixFeature
    {
        public static bool IsApplied { get; private set; }

        [HarmonyPatch(typeof(GameRoom), "StartDetective")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            MethodInfo getter = AccessTools.PropertyGetter(typeof(GamePlayer), nameof(GamePlayer.IsAlive));
            MethodInfo safe = AccessTools.Method(typeof(DetectivePhaseFixFeature), nameof(IsAliveNullSafe));

            int hits = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(getter))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = safe;
                    hits++;
                }
            }

            IsApplied = hits > 0;
            if (hits == 0)
                Debug.LogError("[DT_Tools] StartDetective: 未匹配 get_IsAlive，补丁未生效。");

            return codes;
        }

        private static bool IsAliveNullSafe(GamePlayer player) => player != null && player.IsAlive;
    }
}
