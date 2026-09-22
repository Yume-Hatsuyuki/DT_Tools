using HarmonyLib;
using Protocol;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 平板地图显示全员 Pin。去掉原版「白方且存活则不刷新他人」分支。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    [PatchFeature(
        section: "RefreshPlayerPin",
        description: "玩家雷达：平板电脑显示全部玩家位置（白方无法区分黑方/黑幕）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class PlayerRadarFeature
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_GameTablet __instance)
        {
            if (!FeatureGate.Enabled(typeof(PlayerRadarFeature)))
                return true;

            var t = Traverse.Create(__instance);
            if (!t.Field("_init").GetValue<bool>())
                return false;

            if (Managers.Player.MyPlayer == null || Managers.Game.State == EGameState.Trial)
                return false;

            t.Method("RefreshMyPlayerPin").GetValue();
            foreach (Player player in Managers.Player.Players.Values)
                t.Method("RefreshPlayerPin", player).GetValue();

            return false;
        }
    }
}
