using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using global::System.Reflection.Emit;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Features.WhiteSabotageClue;
using HarmonyLib;

namespace DT_Tools.Features.DestroyEvidenceCooldownServer
{
    [HarmonyPatch]
    [PatchFeature(
        "破坏线索冷却（房主）：调整多久可以破坏一次线索（原版 30 秒，0=无冷却），黑方/黑幕/白方路径一并生效，客户端倒计时自动跟随。单次破坏读条时长见客户端功能「DestroyEvidence」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameSabotage | FeatureTag.GameEvidence,
        Author = "梦初雪")]
    public sealed class DestroyEvidenceCooldownServerFeature
    {
        [Config("破坏线索冷却间隔（秒）：多久可以再次破坏线索（服务端判定）。0=无冷却。原版 30。单次破坏的读条时长是客户端功能「DestroyEvidence」，两者独立。")]
        public static int Interval = 30;

        internal static Harmony SelfHarmony;

        private static bool _patched;

        private static void OnPatched()
        {
            SelfHarmony ??= new Harmony("DT_Tools.DestroyEvidenceCooldownServer");
            if (Engine.Enabled<DestroyEvidenceCooldownServerFeature>())
                ApplyPatches();
            else
                RemovePatches();
        }

        private static void OnEnabled() => ApplyPatches();

        private static void OnDisabled() => RemovePatches();

        private static void ApplyPatches()
        {
            if (_patched) return;
            SelfHarmony ??= new Harmony("DT_Tools.DestroyEvidenceCooldownServer");
            SelfHarmony.PatchAll(typeof(DestroyEvidenceCooldownServerFeature));
            _patched = true;
            Log.Info<DestroyEvidenceCooldownServerFeature>("Transpiler 已挂载（原方法 + 白方复刻）");
        }

        private static void RemovePatches()
        {
            if (SelfHarmony == null) return;
            SelfHarmony.UnpatchSelf();
            _patched = false;
            Log.Info<DestroyEvidenceCooldownServerFeature>("Transpiler 已卸载");
        }

        [HarmonyPatch(typeof(Server.Game.Device), nameof(Server.Game.Device.DestroyEvidence))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TranspileOriginal(IEnumerable<CodeInstruction> instructions)
            => Replace(instructions, "Device.DestroyEvidence（黑方/黑幕路径）", "Server.Game/Device.cs:224/225/231");

        [HarmonyPatch(typeof(WhiteSabotageClueLogic), nameof(WhiteSabotageClueLogic.DestroyEvidence))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TranspileWhiteReplica(IEnumerable<CodeInstruction> instructions)
            => Replace(instructions, "WhiteSabotageClueLogic.DestroyEvidence（白方复刻路径）", "WhiteSabotageClue/Logic.cs:28/29/35");

        private static IEnumerable<CodeInstruction> Replace(
            IEnumerable<CodeInstruction> instructions, string label, string anchors)
        {
            var codes = instructions.ToList();
            FieldInfo interval = AccessTools.Field(
                typeof(DestroyEvidenceCooldownServerFeature), nameof(Interval));

            int hits = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (!IsLdcOf(codes[i], 30))
                    continue;
                codes[i].opcode = OpCodes.Ldsfld;
                codes[i].operand = interval;
                hits++;
            }

            if (hits != 3)
                Log.Error<DestroyEvidenceCooldownServerFeature>(
                    $"{label}: 冷却常量 30 匹配到 {hits}/3 处，ldc 序列 [{DumpLdc(codes)}]（锚点：{anchors}）");
            return codes;
        }

        private static bool IsLdcOf(CodeInstruction code, int value)
            => (code.opcode == OpCodes.Ldc_I4_S || code.opcode == OpCodes.Ldc_I4)
               && (code.operand is int i && i == value || code.operand is sbyte b && b == value);

        private static string DumpLdc(IEnumerable<CodeInstruction> codes)
            => string.Join(" | ", codes
                .Where(c => c.opcode.Name.StartsWith("ldc.i4", StringComparison.Ordinal))
                .Select(c => $"{c.opcode.Name}={c.operand}({c.operand?.GetType().Name ?? "null"})"));
    }
}
