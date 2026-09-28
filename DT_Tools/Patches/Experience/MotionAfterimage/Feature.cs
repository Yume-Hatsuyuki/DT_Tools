using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.MotionAfterimage
{
    /// <summary>
    /// 运动残影：对齐莉莉安娜 SuperRazer（FlashVfx）的 ColorPlayerShadow 材质，
    /// 按间隔持续刷出短命淡出剪影（站立也会刷，类似英雄联盟疾跑残影）。
    /// Survive / Detective 地图内均生效；Despawn 后保留黑色最后一帧，离开地图阶段清除。
    /// </summary>
    [PatchFeature(
        "运动残影：地图内（生存/调查）按间隔刷彩色淡出剪影；死亡后保留黑色最后一帧。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class MotionAfterimageFeature
    {
        // ---- 阶段开关 ----

        [Config("生存阶段启用运动残影。")]
        public static bool EnableInSurvive = true;

        [Config("调查（侦探）阶段启用运动残影。")]
        public static bool EnableInDetective = true;

        [Config("显示自己的运动残影（独立开关）。")]
        public static bool IncludeSelf = true;

        [Config("保留死亡前黑色定格残影（Survive/Detective）。")]
        public static bool ShowDeathResidual = true;

        [Config("显示自己的死亡定格残影（独立开关；挂钩 S_DEAD / Game.Dead）。")]
        public static bool IncludeSelfDeathResidual = true;

        // ---- 刷取与淡出 ----

        [Config("刷残影间隔（秒）。越小越密。", Min = 0.05f, Max = 1f)]
        public static float SpawnInterval = 0.1f;

        [Config("单条残影淡出时长（秒）。", Min = 0.15f, Max = 3f)]
        public static float FadeDuration = 0.7f;

        [Config("每名玩家同时存在的活体残影上限。", Min = 1, Max = 24)]
        public static int MaxPerPlayer = 12;

        [Config("活体残影起始透明度。", Min = 0.1f, Max = 1f)]
        public static float LiveStartAlpha = 0.9f;

        [Config("使用偏蓝染色（关则接近原版 ColorPlayerShadow 白剪影）。")]
        public static bool UseBlueTint = true;

        // ---- 排序 ----

        [Config("活体残影 SortingOrder（原版 FlashVfx=250）。", Min = -100, Max = 500)]
        public static int SortingOrder = 250;

        [Config("死亡黑色定格 SortingOrder（与活体一致，建议 250）。", Min = -100, Max = 500)]
        public static int DeathSortingOrder = 250;

        private static void OnEnabled() => MotionAfterimageLogic.Reset();

        private static void OnDisabled() => MotionAfterimageLogic.ClearAll();
    }
}
