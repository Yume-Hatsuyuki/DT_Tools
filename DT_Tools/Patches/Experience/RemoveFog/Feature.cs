using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.RemoveFog
{
    /// <summary>移除迷雾：关闭房间阴影投射物。</summary>
    // 注意：功能类声明为 sealed class（而非 static class）——Engine.Enabled<T> 需要它作类型参数；
    // 成员全部 static。
    [PatchFeature(
        "移除迷雾：移除房间阴影效果。热切换不即时生效：开关都在下一次幽灵视觉变化" +
        "（OnGhostVisualChanged，0.1.15b MyPlayer.cs:1889）时才应用到房间阴影，建议在对局外切换。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class RemoveFogFeature
    {
    }
}
