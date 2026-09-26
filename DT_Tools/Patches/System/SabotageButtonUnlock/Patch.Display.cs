using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>
    /// GetInteractSabotageMessageBase 前缀整替（public，0.1.15b DeviceBase.cs:389-403）：
    /// 原版 Dark/Black 之外的颜色直接返回空——档位放行时改走 EvaluateMessage
    /// （存活/设备态校验与键有效标志与原版一致）；Dark/Black 交给原版逻辑。
    /// UseSabotageBase（DeviceBase.cs:383）以本方法返回值 + 键有效标志判定触发，
    /// 放开后按键即走设备各自的 UseSabotage（门发包 / 电闸开弹窗）。
    /// </summary>
    [HarmonyPatch(typeof(DeviceBase), nameof(DeviceBase.GetInteractSabotageMessageBase))]
    internal static class SabotageMessageBasePatch
    {
        private static bool Prefix(DeviceBase __instance, int index, ref string __result)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;

            var color = Managers.Player.MyPlayer.Color;
            if (color == EPlayerColor.Dark || color == EPlayerColor.Black)
                return true;
            if (!SabotageButtonUnlockLogic.ColorAllowedForDevice(__instance.DeviceType, color))
            {
                __result = "";
                return false;
            }
            __result = SabotageButtonUnlockLogic.EvaluateMessage(__instance, index);
            return false;
        }
    }

    /// <summary>
    /// ShowInteractSabotageText 前缀整替（私有方法，字符串定位：0.1.15b UI_GameScene.cs:1355）：
    /// 交互提示按钮的显示判定整替，实现见 Logic.RefreshSabotagePrompt。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameScene), "ShowInteractSabotageText")]
    internal static class SabotagePromptPatch
    {
        private static bool Prefix(UI_GameScene __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;

            SabotageButtonUnlockLogic.RefreshSabotagePrompt(__instance);
            return false;
        }
    }
}
