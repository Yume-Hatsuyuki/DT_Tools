using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.PassKnifeRange
{
    /// <summary>
    /// 递刀距离：GetHandWeaponTarget 中原版硬编码 224f（0.1.15b MyPlayer.cs:2191）→ 可配置；
    /// 仍排除 Hide / KnownBlackIds（已知的黑幕）。最近玩家搜索与攻击距离（AttackRange）
    /// 共用 Game/NearestTargetFinder。
    /// </summary>
    [PatchFeature(
        "递刀距离：可修改黑幕最大递交武器的距离（默认 224）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class PassKnifeRangeFeature
    {
        [Config("递交武器可触及的最大距离，游戏默认为 224。", Min = 0f)]
        public static float Range = 224f;
    }
}
