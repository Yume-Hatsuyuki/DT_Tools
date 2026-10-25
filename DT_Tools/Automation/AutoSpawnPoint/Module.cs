using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    [AutomationModule("开局指定出生点",
        "进入生存阶段后传送到 StartPosList 出生点（下拉）或自定义坐标。",
        side: FeatureSide.Client,
        Tags = FeatureTag.SysAutomation | FeatureTag.PlayerMove,
        Author = "梦初雪")]
    public sealed class AutoSpawnPointModule
    {
        [Config("选择方式：Index=按出生点序号，Custom=使用下方 PosX / PosY 自定义坐标。")]
        public static AutoSpawnMode Mode = AutoSpawnMode.Index;

        [Config("出生点序号（1-based），Mode=Index 时生效；WebUI 以下拉展示坐标与房间。")]
        public static int SpawnIndex = 1;

        [Config("自定义坐标 X，Mode=Custom 时生效。")]
        public static float PosX = 0f;

        [Config("自定义坐标 Y，Mode=Custom 时生效。")]
        public static float PosY = 0f;

        [Config("进入 Survive 后延迟多少秒再传送。", Min = 0, Max = 120)]
        public static float DelaySeconds = 15f;

        public static void Tick(bool hostEnabled)
        {
            if (!hostEnabled || !Engine.Enabled<AutoSpawnPointModule>())
            {
                AutoSpawnPointState.Reset();
                return;
            }
            AutoSpawnPointTrigger.Tick();
        }

        private static void OnLoaded()
            => OptionProviders.Bind(
                Engine.SectionOf<AutoSpawnPointModule>(),
                "SpawnIndex",
                AutoSpawnPointLogic.SpawnOptions);
    }
}
