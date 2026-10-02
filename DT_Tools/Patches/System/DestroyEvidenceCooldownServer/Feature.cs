using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using global::System.Reflection.Emit;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Patches.System.WhiteSabotageClue;
using HarmonyLib;

namespace DT_Tools.Patches.System.DestroyEvidenceCooldownServer
{
    /// <summary>
    /// 破坏线索冷却（房主侧）：调整"多久可以破坏一次线索"（服务端判定频率），与客户端
    /// 「DestroyEvidence」功能（单次破坏读条多少秒）是两回事。客户端倒计时由服务端
    /// S_COOLTIME_DESTROY_EVIDENCE 包值驱动（0.1.16b PacketHandler.cs:776），自动跟随。
    /// 冷却常量 30 在两条执行路径各一份、三处一组，必须同值：
    /// - 黑方/黑幕路径：Device.DestroyEvidence（Server.Game/Device.cs:224 记账 / :225 恢复任务 / :231 下发包）
    /// - 白方路径：「WhiteSabotageClue」的放行前缀整段复刻了原方法（Logic.cs:28/29/35），
    ///   白方销毁不走原方法——只改原方法管不住白方，故两条路径一并替换。
    /// 迁移快照剩余时长（Server.Game/SnapshotCodec.cs:99 用 EndTick-surviveTime 反推）自动跟随。
    /// Transpiler 是装载期 IL 改写、无法按调用门闩：自管 Harmony（存在 SelfHarmony 字段
    /// 引擎即跳过本命名空间自动挂载），Enabled 热切换时挂/卸，关闭即全部恢复原版 30 秒。
    /// 配置热改实时生效（ldsfld 每次执行都读当前值，无需重新 Transpile）。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "破坏线索冷却（房主）：调整多久可以破坏一次线索（原版 30 秒，0=无冷却），黑方/黑幕/白方路径一并生效，客户端倒计时自动跟随。单次破坏读条时长见客户端功能「DestroyEvidence」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class DestroyEvidenceCooldownServerFeature
    {
        [Config("破坏线索冷却间隔（秒）：多久可以再次破坏线索（服务端判定）。0=无冷却。原版 30。单次破坏的读条时长是客户端功能「DestroyEvidence」，两者独立。")]
        public static int Interval = 30;

        /// <summary>存在本字段时引擎跳过本命名空间自动挂载，Transpiler 由本类按 Enabled 自管挂卸。</summary>
        internal static Harmony SelfHarmony;

        private static bool _patched;

        /// <summary>挂载流程完成后：按当前 Enabled 决定是否挂上 Transpiler。</summary>
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

        // 原方法冷却常量 30 共 3 处：0.1.16b Server.Game/Device.cs:224（EndTick）/ :225（恢复任务）/ :231（S_COOLTIME 包）
        [HarmonyPatch(typeof(Server.Game.Device), nameof(Server.Game.Device.DestroyEvidence))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TranspileOriginal(IEnumerable<CodeInstruction> instructions)
            => Replace(instructions, "Device.DestroyEvidence（黑方/黑幕路径）", "Server.Game/Device.cs:224/225/231");

        // 白方复刻序列（整段镜像原方法，差异仅留痕身份）同样三处 30：Logic.cs:28（EndTick）/ :29（恢复任务）/ :35（S_COOLTIME 包）
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

        // Harmony 的 ldc.i4.s operand 运行时装箱为 sbyte（ldc.i4 为 int），两种都必须接——
        // Cecil 静态 dump 只证明指令存在，不能证明运行时装箱类型
        private static bool IsLdcOf(CodeInstruction code, int value)
            => (code.opcode == OpCodes.Ldc_I4_S || code.opcode == OpCodes.Ldc_I4)
               && (code.operand is int i && i == value || code.operand is sbyte b && b == value);

        // 失配自诊断：dump 方法内全部 ldc.i4*（值+运行时装箱类型），升级后锚点漂移一眼定位
        private static string DumpLdc(IEnumerable<CodeInstruction> codes)
            => string.Join(" | ", codes
                .Where(c => c.opcode.Name.StartsWith("ldc.i4", StringComparison.Ordinal))
                .Select(c => $"{c.opcode.Name}={c.operand}({c.operand?.GetType().Name ?? "null"})"));
    }
}
