using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.CanShowLie
{
    /// <summary>
    /// 任意身份伪证：去掉 CanShowLie / IsMyPlayerBlack 的 Black 限制；
    /// 非 Black 无 S_CURRENT_MAP 时用缓存的 S_INIT_MAP 补全 RoomObjectDict。
    /// </summary>
    [PatchFeature(
        "伪证：审判讨论阶段任何身份都可使用伪证，可选内容与黑方一致。\n二阶堂希罗：我当时睡得可香了。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class CanShowLieFeature
    {
    }
}
