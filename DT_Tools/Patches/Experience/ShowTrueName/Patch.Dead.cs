using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.ShowTrueName
{
    /// <summary>
    /// Dead 后置：可选「死后仍显」。原版 Dead 会关闭本机 NameTag
    /// （0.1.15b GameManagerEX.cs:333，SetActive(false) 位于 :344）。
    /// 公共方法（nameof 定位）。
    /// </summary>
    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Dead))]
    internal static class ShowTrueNameDeadPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<ShowTrueNameFeature>())
                return;

            if (!ShowTrueNameFeature.ShowWhileDead)
                return;
            Player my = Managers.Player.MyPlayer;
            if (my?.NameTag != null)
                my.NameTag.gameObject.SetActive(true);
        }
    }
}
