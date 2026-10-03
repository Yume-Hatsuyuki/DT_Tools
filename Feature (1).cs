using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Experience.BenjaminPip
{
    /// <summary>
    /// 本杰明监视窗（画中画）：屏幕角落常驻小窗，实时显示本杰明（Rin 的 Marionette 召唤物）
    /// 视野画面——用一个跟随本杰明位置的正交小相机渲染到小窗，画面范围正好等于本杰明探测范围
    /// （448×410.7，与原版检测一致），有人进入范围画面里就能直接看到，不用按 R 附身。
    /// 相机与主视角同层渲染，隔墙同样看不见（与原版本杰明视线判定一致），纯本地显示。
    /// </summary>
    [PatchFeature(
        "本杰明监视窗：屏幕角落画中画实时显示本杰明视野画面（范围=探测范围），有人来直接看到，不用按 R 附身。",
        defaultEnabled: true)]
    public sealed class BenjaminPipFeature
    {
        [Config("监视窗宽度（像素）：画面小窗宽度，高度按探测范围比例自动计算。", Min = 160f, Max = 640f)]
        public static float Width = 320f;

        [Config("窗口位置：0=左上 1=右上 2=左下 3=右下。", Min = 0f, Max = 3f)]
        public static int Corner = 3;
    }
}
