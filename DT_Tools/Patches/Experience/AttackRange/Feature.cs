using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.AttackRange
{
    /// <summary>
    /// 黑方攻击距离：GetTargetPlayer 中原版硬编码 224f（0.1.15b MyPlayer.cs:2162）→ 可配置。
    /// 最近玩家搜索与递刀距离（PassKnifeRange）共用 Game/NearestTargetFinder。
    /// </summary>
    [PatchFeature(
        "黑方攻击距离：可修改最大攻击距离（默认 224）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class AttackRangeFeature
    {
        [Config("黑方攻击可命中的最大距离，游戏默认为 224。", Min = 0f)]
        public static float Range = 224f;
    }
}
