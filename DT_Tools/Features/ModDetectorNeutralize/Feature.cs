using System;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Features.ModDetectorNeutralize
{
    [PatchFeature(
        "屏蔽 0.1.21 起的 MOD 检测：对游戏恒返回「未改装」并跳过后台代码指纹扫描；可选拦截对局上报（MatchStatsReporter 的 Discord webhook）。P2P 自建房不再被误标记。旧版本自动跳过。",
        defaultEnabled: true,
        side: FeatureSide.Both,
        Tags = FeatureTag.SysBackend,
        Author = "梦初雪")]
    public sealed class ModDetectorNeutralizeFeature
    {
        [Config("屏蔽 MOD 检测：对游戏恒返回「未改装」（ModDetector.IsModded 不再执行，因此不写检测原因日志），跳过后台代码指纹扫描，并且本机生成的对局摘要不再因角色异常标记 modded。")]
        public static bool HideFromModDetector = true;

        [Config("拦截对局上报：跳过 MatchStatsReporter.Send，不再向开发者 Discord webhook 发送对局统计（含 modded 标记）。")]
        public static bool BlockMatchStatsReport = true;

        internal static Harmony SelfHarmony;

        private static bool _patched;

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

        private static int HideFromModDetectorPatches()
        {
            var type = AccessTools.TypeByName("ModDetector");
            if (type == null)
            {
                Log.Warn<ModDetectorNeutralizeFeature>("ModDetector 不存在（游戏版本 < 0.1.21），跳过屏蔽。");
                return 0;
            }

            int mounted = 0;
            mounted += PatchMethod(type, "IsModded", nameof(SkipIsModded), prefix: true, "ModDetector.IsModded（0.1.21a ModDetector.cs:58）");
            mounted += PatchMethod(type, "RunCodeCheck", nameof(SkipRunCodeCheck), prefix: true, "ModDetector.RunCodeCheck（0.1.21a ModDetector.cs:119）");
            mounted += PatchMethod(type, "StartCodeCheck", nameof(SkipStartCodeCheck), prefix: true, "ModDetector.StartCodeCheck（0.1.21a ModDetector.cs:42）");
            mounted += PatchMethod(type, "MarkChanged", nameof(SkipMarkChanged), prefix: true, "ModDetector.MarkChanged（0.1.21c ModDetector.cs:172）");

            var reporter = AccessTools.TypeByName("Server.Game.MatchStatsReporter");
            if (reporter == null)
                Log.Warn<ModDetectorNeutralizeFeature>("MatchStatsReporter 不存在（游戏版本 < 0.1.21），跳过角色异常判定屏蔽。");
            else
                mounted += PatchMethod(reporter, "IsAbnormalCharacter", nameof(SkipIsAbnormalCharacter), prefix: true, "MatchStatsReporter.IsAbnormalCharacter（0.1.21c MatchStatsReporter.cs:161）");
            return mounted;
        }

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

        private static bool SkipIsModded(ref bool __result)
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            __result = false;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 ModDetector.IsModded：未执行检测，也不记录检测原因");
            return false;
        }

        private static bool SkipMarkChanged()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            return false;
        }

        private static bool SkipIsAbnormalCharacter(ref bool __result)
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            __result = false;
            return false;
        }

        private static bool SkipStartCodeCheck()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 ModDetector.StartCodeCheck：跳过启动后台代码指纹扫描");
            return false;
        }

        private static bool SkipRunCodeCheck()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 ModDetector.RunCodeCheck：跳过全盘代码指纹扫描");
            return false;
        }

        private static bool SkipMatchStatsSend()
        {
            if (!Engine.Enabled<ModDetectorNeutralizeFeature>())
                return true;
            Log.Info<ModDetectorNeutralizeFeature>("已拦截 MatchStatsReporter.Send：停止 Discord 对局上报");
            return false;
        }
    }
}
