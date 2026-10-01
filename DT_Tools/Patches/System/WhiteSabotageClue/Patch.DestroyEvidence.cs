using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.WhiteSabotageClue
{
    /// <summary>
    /// 白方销毁证据放行：Device.DestroyEvidence 前缀（public，0.1.15b Server.Game/Device.cs:217）。
    /// 原版门禁硬编码仅黑方/黑幕（:219 的颜色判断），白方的销毁请求会被静默拒绝；
    /// 本前缀在白方请求且其余门禁条件（存活/设备有证据/个人冷却，与原版同式）满足时
    /// 接管执行白方销毁序列（Logic.DestroyEvidence，痕迹记真实身份），其余一律交回
    /// 原版——黑方/黑幕路径零改动，关闭功能即原版门禁。
    /// DestroyEvidence 为非虚方法且由 DeviceManager 分发（Server.Game/HostPacketHandler.cs:747），
    /// 补丁打在基类即可覆盖全部设备。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.Device), nameof(Server.Game.Device.DestroyEvidence))]
    internal static class WhiteDestroyEvidencePatch
    {
        private static bool Prefix(Server.Game.Device __instance, Server.Game.Player player)
        {
            if (!Engine.Enabled<WhiteSabotageClueFeature>() || !WhiteSabotageClueFeature.DestroyEvidence)
                return true;    // 关闭/未勾选：原版门禁（仅黑方/黑幕）原样生效
            if (player == null || player.Color != EPlayerColor.White)
                return true;    // 黑方/黑幕走原版销毁路径
            if (!player.IsAlive || __instance.DeviceData == null
                || __instance.DeviceData.WhatType == EWhatType.WNone || !player.CanDestroyEvidence)
                return true;    // 其余门禁不满足：交回原版（对白方同样是静默拒绝）

            WhiteSabotageClueLogic.DestroyEvidence(__instance, player);
            return false;
        }
    }
}
