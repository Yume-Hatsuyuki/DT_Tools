using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using global::System.Reflection.Emit;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Features.DetectivePhaseFix
{
    [HarmonyPatch]
    [PatchFeature(
        "调查阶段补丁：修复无凶手时 StartDetective 空引用；/enter_detective 依赖此补丁。关闭时会卸载 Transpiler，无需重启。",
        defaultEnabled: true,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameRound,
        Author = "梦初雪")]
    public sealed class DetectivePhaseFixFeature
    {
        internal static Harmony SelfHarmony;

        public static bool IsApplied { get; private set; }

        private static bool _patched;

        private static void OnPatched()
        {
            SelfHarmony ??= new Harmony("DT_Tools.DetectivePhaseFix");
            if (Engine.Enabled<DetectivePhaseFixFeature>())
                ApplyPatches();
            else
                RemovePatches();
        }

        private static void OnEnabled() => ApplyPatches();

        private static void OnDisabled() => RemovePatches();

        private static void ApplyPatches()
        {
            if (_patched) return;
            SelfHarmony ??= new Harmony("DT_Tools.DetectivePhaseFix");
            SelfHarmony.PatchAll(typeof(DetectivePhaseFixFeature));
            _patched = true;
            Log.Info<DetectivePhaseFixFeature>("Transpiler 已挂载");
        }

        private static void RemovePatches()
        {
            if (SelfHarmony == null) return;
            SelfHarmony.UnpatchSelf();
            _patched = false;
            IsApplied = false;
            Log.Info<DetectivePhaseFixFeature>("Transpiler 已卸载");
        }

        [HarmonyPatch(typeof(GameRoom), "StartDetective")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            MethodInfo getter = AccessTools.PropertyGetter(
                typeof(Server.Game.Player), nameof(Server.Game.Player.IsAlive));
            MethodInfo safe = AccessTools.Method(
                typeof(DetectivePhaseFixFeature), nameof(IsAliveNullSafe));

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
                Log.Error<DetectivePhaseFixFeature>("StartDetective: 未匹配 get_IsAlive，补丁未生效。");

            return codes;
        }

        private static bool IsAliveNullSafe(Server.Game.Player player) => player != null && player.IsAlive;
    }
}
