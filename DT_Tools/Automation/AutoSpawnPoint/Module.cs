using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    /// <summary>进入 Survive 后传送到指定出生点或自定义坐标（与 /beacon 同源）。</summary>
    [AutomationModule("开局指定出生点", "进入生存阶段后传送到 StartPosList 出生点（下拉）或自定义坐标。", Author = "梦初雪")]
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

        /// <summary>
        /// 统一节拍入口。注意：在对局中（已处于 Survive）热启用本模块时，
        /// State.Reset 已把 LastState 归 NoneState、DoneThisRound=false，下一拍
        /// Trigger 会视作"新进入 Survive"，立即按 DelaySeconds 计时并触发本局传送
        /// ——与 AutoAcquireWeapon / AutoPickCharacter 共有的统一模式，属预期行为。
        /// </summary>
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
