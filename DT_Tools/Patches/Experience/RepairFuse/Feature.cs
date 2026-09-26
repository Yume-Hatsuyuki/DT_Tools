using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.RepairFuse
{
    /// <summary>
    /// 修电读条。Fusebox.Interact 原版总时长 10s（0.1.15b Fusebox.cs:46），
    /// StateList[0]==9999 时可改；剩余不超过服务端 10-已修。
    /// </summary>
    [PatchFeature(
        "专业电工：可修改修电闸默认读条时长（默认 10s，优先结算来自服务端的剩余时间）。",
        defaultEnabled: false,
        Author = "梦初雪")]
    public sealed class RepairFuseFeature
    {
        [Config("修电闸默认读条时长（秒）。建议 >= 0.1；不会超过服务端剩余时间。", Min = 0.1f)]
        public static float CastingTime = 10f;
    }
}
