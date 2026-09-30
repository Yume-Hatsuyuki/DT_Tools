using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.ShowTrueName
{
    /// <summary>
    /// 吾之真名：本机名字常显。ChangeMyPlayer 进对局后原版会关掉自己的 NameTag
    /// （0.1.15b PlayerManager.cs:635），此处强制打开；可选死后仍显（GameManagerEX.Dead，:333）。
    /// </summary>
    [PatchFeature(
        "吾之真名：进入游戏后头顶名字持续可见。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class ShowTrueNameFeature
    {
        [Config("死亡后是否继续显示自己头顶名字（默认 false）。")]
        public static bool ShowWhileDead = false;
    }
}
