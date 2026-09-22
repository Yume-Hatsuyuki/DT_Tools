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
    /// Transpiler 使用独立 Harmony 实例，Enabled 热切换时 Patch/Unpatch，关闭即可恢复原 IL。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "StartDetective",
        description: "调查阶段补丁：修复无凶手时 StartDetective 空引用；/enter_detective 依赖此补丁。关闭时会卸载 Transpiler，无需重启。",
        defaultEnabled: true,
        side: FeatureSide.Host,
        author: "梦初雪")]
    internal static class DetectivePhaseFixFeature
    {
        /// <summary>供 PatchLoader 识别为自行管理 Harmony。</summary>
        internal static Harmony SelfHarmony;

        public static bool IsApplied { get; private set; }

        private static bool _patched;

        /// <summary>启动：按当前 Enabled 决定是否挂上 Transpiler。</summary>
        public static void OnPatched()
        {
            SelfHarmony ??= new Harmony("DT_Tools.DetectivePhaseFix");
            if (FeatureGate.Enabled(typeof(DetectivePhaseFixFeature)))
                ApplyPatches();
            else
                RemovePatches();
        }

        public static void OnEnabled() => ApplyPatches();

        public static void OnDisabled() => RemovePatches();

        private static void ApplyPatches()
        {
            if (_patched) return;
            SelfHarmony ??= new Harmony("DT_Tools.DetectivePhaseFix");
            SelfHarmony.PatchAll(typeof(DetectivePhaseFixFeature));
            _patched = true;
            FeatureLogRegistry.Info("StartDetective", "Transpiler 已挂载");
        }

        private static void RemovePatches()
        {
            if (SelfHarmony == null) return;
            SelfHarmony.UnpatchSelf();
            _patched = false;
            IsApplied = false;
            FeatureLogRegistry.Info("StartDetective", "Transpiler 已卸载");
        }

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
