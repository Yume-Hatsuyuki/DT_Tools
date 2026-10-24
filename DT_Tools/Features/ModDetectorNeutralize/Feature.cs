using System;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Features.ModDetectorNeutralize
{
    /// <summary>
    /// 屏蔽游戏 0.1.21 起新增的 MOD 检测（ModDetector）与对局上报（MatchStatsReporter）：
    /// 让本机对游戏恒返回「未改装」、跳过其后台代码指纹扫描，并可选拦截其 Discord 对局上报，
    /// 避免 P2P 自建房玩家被误标记/误报。
    /// </summary>
    /// <remarks>
    /// 背景（源码锚点见各方法注释，0.1.21a）：游戏全局新增 ModDetector（全局命名空间，ModDetector.cs:12），
    /// 启动期检测本机改装——
    /// 1) 扫根目录注入文件/目录（winhttp.dll、version.dll、doorstop_config.ini、BepInEx、MelonLoader，:16-20）；
    /// 2) 扫已加载程序集中 BepInEx / 0Harmony / MonoMod 等加载器前缀（:16）；
    /// 3) 后台 Task 按 code_manifest.txt 对全部代码文件做 SHA256 指纹比对（:42-54, :119）。
    /// 任一命中 IsModded()==true（:58），经 C_ENTER_GAME.IsModded / SnapshotPlayer.IsModded 进入对局，
    /// 并随 MatchStatsReporter 在对局结算时上报到开发者 Discord webhook（MatchStatsReporter.cs:59, :51）。
    /// 本机只要装了 BepInEx 就必然命中 1/2，等于每个 DT_Tools 用户都会被标 modded 并上报——
    /// 这是「误伤」的来源。本功能在本机上的覆盖范围：
    /// 1) IsModded() 前缀直接返回 false，不执行原方法体，因此检测原因不写入 Player.log / log_*.txt；
    /// 2) 指纹扫描（StartCodeCheck / RunCodeCheck）及其不一致记录（MarkChanged）均拦截；
    /// 3) 本机生成的对局摘要中，角色异常（梅德琳类型或 ID 不在角色表）不计为 modded（IsAbnormalCharacter 恒为否）；
    /// 4) 对局上报（MatchStatsReporter.Send）可选拦截。
    /// 不在本功能范围内：其他客户端对本机角色的库存目击上报（CosmeticSightingReporter），本机无法拦截。
    ///
    /// 版本兼容：ModDetector / MatchStatsReporter 仅 0.1.21+ 存在，本功能用反射按版本动态挂载。
    /// 旧版本缺类时静默跳过（不报错、不影响其余功能）；新版本存在时生效。因此无需升级编译
    /// 目标，对 0.1.20a 及更早版本完全透明。
    ///
    /// 说明：本功能只影响「是否被标记为改装」这一上报位，不改动任何对局玩法。
    /// </remarks>
    [PatchFeature(
        "屏蔽 0.1.21 起的 MOD 检测：对游戏恒返回「未改装」并跳过后台代码指纹扫描；可选拦截对局上报（MatchStatsReporter 的 Discord webhook）。P2P 自建房不再被误标记。旧版本自动跳过。",
        defaultEnabled: true,
        side: FeatureSide.Both,
        Tags = FeatureTag.SysBackend,
        Author = "梦初雪")]
    public sealed class ModDetectorNeutralizeFeature
    {
        /// <summary>
        /// 屏蔽 ModDetector：IsModded() 恒 false，并跳过其后台代码指纹扫描。
        /// </summary>
        [Config("屏蔽 MOD 检测：对游戏恒返回「未改装」（ModDetector.IsModded 不再执行，因此不写检测原因日志），跳过后台代码指纹扫描，并且本机生成的对局摘要不再因角色异常标记 modded。")]
        public static bool HideFromModDetector = true;

        /// <summary>
        /// 拦截对局上报：跳过 MatchStatsReporter.Send，不再向开发者 Discord webhook 发送对局统计（含 modded 标记）。
        /// </summary>
        [Config("拦截对局上报：跳过 MatchStatsReporter.Send，不再向开发者 Discord webhook 发送对局统计（含 modded 标记）。")]
        public static bool BlockMatchStatsReport = true;

        /// <summary>
        /// 存在本字段时引擎跳过自动挂载，由本类按运行版本与 Enabled 自管挂卸。
        /// </summary>
        internal static Harmony SelfHarmony;

        private static bool _patched;

        /// <summary>
        /// 挂载流程完成后：按当前 Enabled 与配置决定挂哪些补丁。
        /// </summary>
        private static void OnPatched()
        {
            SelfHarmony ??= new Harmony("DT_Tools.ModDetectorNeutralize");
            if (Engine.Enabled<ModDetectorNeutralizeFeature>())
                ApplyPatches();
            else
                RemovePatches();
        }

        private static void OnEnabled() => ApplyPatches();

        private static void OnDisabled() => RemovePatches();

        private static void ApplyPatches()
        {
            if (_patched) return;
            SelfHarmony ??= new Harmony("DT_Tools.ModDetectorNeutralize");

            int mounted = 0;
            if (HideFromModDetector)
                mounted += HideFromModDetectorPatches();
            if (BlockMatchStatsReport)
                mounted += BlockMatchStatsReportPatches();

            _patched = true;
            Log.Info<ModDetectorNeutralizeFeature>(
                $"已挂载 {mounted} 个补丁（HideFromModDetector={HideFromModDetector}, BlockMatchStatsReport={BlockMatchStatsReport}）。拦截发生时各打一条 Info 日志，可在插件日志/WebUI 查看。");
        }

        /// <summary>
        /// ModDetector 相关补丁：IsModded 恒 false + 跳过启动期指纹扫描。
        /// </summary>
        private static int HideFromModDetectorPatches()
        {
            var type = AccessTools.TypeByName("ModDetector");
            if (type == null)
            {
                Log.Warn<ModDetectorNeutralizeFeature>("ModDetector 不存在（游戏版本 < 0.1.21），跳过屏蔽。");
                return 0;
            }

            int mounted = 0;
            // IsModded 前缀直接跳过原方法：原方法体内的检测与 Debug.Log（检测原因）一并不执行。
            mounted += PatchMethod(type, "IsModded", nameof(SkipIsModded), prefix: true, "ModDetector.IsModded（0.1.21a ModDetector.cs:58）");
            // StartCodeCheck 由 Unity [RuntimeInitializeOnLoadMethod] 触发（:42），可能先于本补丁执行；
            // 其后台 Task 内对 RunCodeCheck 的调用发生在异步线程、晚于补丁挂载，故 RunCodeCheck
            // 前缀是拦下指纹扫描的可靠落点（:119），StartCodeCheck 前缀为尽力而为。
            mounted += PatchMethod(type, "RunCodeCheck", nameof(SkipRunCodeCheck), prefix: true, "ModDetector.RunCodeCheck（0.1.21a ModDetector.cs:119）");
            mounted += PatchMethod(type, "StartCodeCheck", nameof(SkipStartCodeCheck), prefix: true, "ModDetector.StartCodeCheck（0.1.21a ModDetector.cs:42）");
            // RunCodeCheck 若在补丁挂载前已开始，仍会经 MarkChanged 写入含文件名的原因（0.1.21c ModDetector.cs:172-176），此处兜底。
            mounted += PatchMethod(type, "MarkChanged", nameof(SkipMarkChanged), prefix: true, "ModDetector.MarkChanged（0.1.21c ModDetector.cs:172）");

            // 对局摘要的 modded 还会由角色类型判定（0.1.21c MatchStatsReporter.cs:92），与 IsModded 无关，需单独拦截。
            var reporter = AccessTools.TypeByName("Server.Game.MatchStatsReporter");
            if (reporter == null)
                Log.Warn<ModDetectorNeutralizeFeature>("MatchStatsReporter 不存在（游戏版本 < 0.1.21），跳过角色异常判定屏蔽。");
            else
                mounted += PatchMethod(reporter, "IsAbnormalCharacter", nameof(SkipIsAbnormalCharacter), prefix: true, "MatchStatsReporter.IsAbnormalCharacter（0.1.21c MatchStatsReporter.cs:161）");
            return mounted;
        }

        /// <summary>
        /// MatchStatsReporter 相关补丁：跳过对局结算时的 Discord 上报。
        /// </summary>
        private static int BlockMatchStatsReportPatches()
        {
            var type = AccessTools.TypeByName("Server.Game.MatchStatsReporter");
            if (type == null)
            {
                Log.Warn<ModDetectorNeutralizeFeature>("MatchStatsReporter 不存在（游戏版本 < 0.1.21），跳过拦截上报。");
                return 0;
            }
            return PatchMethod(type, "Send", nameof(SkipMatchStatsSend), prefix: true, "MatchStatsReporter.Send（0.1.21a MatchStatsReporter.cs:59）");
        }

        /// <summary>
        /// 解析目标方法并挂前缀/后缀。方法不存在（版本漂移）时告警并跳过，不抛异常、不影响其余补丁。
        /// </summary>
        /// <param name="owner">目标类型（运行时解析，非 typeof 硬编码）。</param>
        /// <param name="methodName">目标方法名，私有/公有均可。</param>
        /// <param name="patchName">本类中承担前缀/后缀的补丁方法名。</param>
        /// <param name="prefix">true=挂前缀（跳过原方法）；false=挂后缀（改写返回值）。</param>
        /// <param name="label">日志定位用的完整方法名与源码锚点。</param>
        private static int PatchMethod(Type owner, string methodName, string patchName, bool prefix, string label)
        {
            var target = AccessTools.Method(owner, methodName);
            if (target == null)
            {
                Log.Warn<ModDetectorNeutralizeFeature>($"{label} 不存在（版本漂移），跳过。");
                return 0;
            }
            var patch = AccessTools.Method(typeof(ModDetectorNeutralizeFeature), patchName);
            if (prefix)
                SelfHarmony.Patch(target, prefix: new HarmonyMethod(patch));
            else
                SelfHarmony.Patch(target, postfix: new HarmonyMethod(patch));
            return 1;
        }

        private static void RemovePatches()
        {
            if (SelfHarmony == null) return;
            SelfHarmony.UnpatchSelf();
            _patched = false;
            Log.Info<ModDetectorNeutralizeFeature>("补丁已卸载");
        }

        /// <summary>
        /// ModDetector.IsModded 前缀：不执行原方法，恒返回「未改装」。检测与检测原因的日志因此都不发生。
        /// </summary>
        private static bool SkipIsModded(ref bool __result)
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            __result = false;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 ModDetector.IsModded：未执行检测，也不记录检测原因");
            return false;
        }

        /// <summary>
        /// ModDetector.MarkChanged 前缀：不记录指纹不一致的原因（原因含文件名，会进入 Player.log）。
        /// </summary>
        private static bool SkipMarkChanged()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            return false;
        }

        /// <summary>
        /// MatchStatsReporter.IsAbnormalCharacter 前缀：角色类型为梅德琳或 ID 不在角色表时，不计为 modded。
        /// 作用于本机生成的对局摘要（本机为房主时），对其他玩家的角色判定同样生效。
        /// </summary>
        private static bool SkipIsAbnormalCharacter(ref bool __result)
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            __result = false;
            return false;
        }

        /// <summary>
        /// 前缀返回 false，跳过原方法（StartCodeCheck）：不再启动后台代码指纹扫描任务，并留痕。
        /// </summary>
        private static bool SkipStartCodeCheck()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 ModDetector.StartCodeCheck：跳过启动后台代码指纹扫描");
            return false;
        }

        /// <summary>
        /// 前缀返回 false，跳过原方法（RunCodeCheck）：不再做全盘代码指纹比对，并留痕。
        /// </summary>
        private static bool SkipRunCodeCheck()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 ModDetector.RunCodeCheck：跳过全盘代码指纹扫描");
            return false;
        }

        /// <summary>
        /// 前缀返回 false，跳过原方法（MatchStatsReporter.Send）：不再向 Discord webhook 上报对局统计，并留痕。
        /// </summary>
        private static bool SkipMatchStatsSend()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 MatchStatsReporter.Send：停止 Discord 对局上报");
            return false;
        }
    }
}
