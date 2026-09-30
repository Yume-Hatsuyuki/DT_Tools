using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using global::System.Reflection.Emit;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Patches.System.DetectivePhaseFix
{
    /// <summary>
    /// StartDetective 开头 black.IsAlive 在 Black==null 时 NRE（0.1.15b GameRoom.cs:2037，
    /// 2041 行触发——同一方法内 SyncAllPlayer 回调对 black 判空，可证原版即为潜在缺陷）。
    /// 将 get_IsAlive 换为 null 安全调用。Transpiler 使用独立 Harmony 实例，
    /// Enabled 热切换时 Patch/Unpatch，关闭即可恢复原 IL，无需重启。
    /// 自管 Harmony：Feature 声明 static Harmony 字段后，引擎跳过本命名空间的自动 PatchAll。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "调查阶段补丁：修复无凶手时 StartDetective 空引用；/enter_detective 依赖此补丁。关闭时会卸载 Transpiler，无需重启。",
        defaultEnabled: true,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class DetectivePhaseFixFeature
    {
        /// <summary>存在本字段时引擎跳过自动挂载，由本类自行管理 Transpiler 的挂载与卸载。</summary>
        internal static Harmony SelfHarmony;

        public static bool IsApplied { get; private set; }

        private static bool _patched;

        /// <summary>挂载流程完成后：按当前 Enabled 决定是否挂上 Transpiler。</summary>
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

        // StartDetective 为私有方法，字符串定位：0.1.15b GameRoom.cs:2037
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
