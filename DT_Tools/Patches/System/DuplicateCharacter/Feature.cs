using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.DuplicateCharacter
{
    /// <summary>
    /// 重复角色：多名玩家可选择同一角色（房主启用即可）。
    /// 选择 / 取消确认只回操作者本人、全员揭晓统一在 PickCharacterTick 收口，
    /// 防止对端收到广播后置灰格子并本地拦截后续重复选择请求。
    /// </summary>
    [PatchFeature(
        "重复角色：多名玩家可选择同一角色（房主启用即可）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class DuplicateCharacterFeature
    {
    }
}
