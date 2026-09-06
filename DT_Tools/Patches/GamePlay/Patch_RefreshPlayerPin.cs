using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   UI_GameTablet::LateUpdate()
    ///
    /// <b>原版效果</b>：
    ///   若 MyPlayer.Color == White 且 IsAlive，则 return，不刷新其他玩家 Pin；
    ///   否则 foreach Players 调用 RefreshPlayerPin。
    ///
    /// <b>修改后效果</b>：
    ///   去掉 White + IsAlive 提前返回，始终 RefreshPlayerPin 全部玩家；
    ///   Trial 阶段仍不刷新。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 重写 LateUpdate 主体。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    [PatchConfig(
        "Enable_RefreshPlayerPin",
        "地图玩家位置：平板电脑地图上显示所有玩家位置（无法区分阵营）。",
        author: "梦初雪")]
    internal static class Patch_RefreshPlayerPin
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_GameTablet __instance)
        {
            var t = Traverse.Create(__instance);

            if (!t.Field("_init").GetValue<bool>())
                return true;

            if (Managers.Player.MyPlayer == null)
                return true;

            if (Managers.Game.State == EGameState.Trial)
                return true;

            t.Method("RefreshMyPlayerPin").GetValue();

            // 原版此处：
            // if (myPlayer.Color == EPlayerColor.White && Managers.Game.IsAlive) return;
            foreach (Player player in Managers.Player.Players.Values)
                t.Method("RefreshPlayerPin", player).GetValue();

            return false;
        }
    }
}
