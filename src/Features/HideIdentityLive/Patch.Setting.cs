// 作用：放行房间内的隐藏身份开关并触发同步，同时维护设置页开关可交互状态；不负责名册消费与展示层。
using System;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.HideIdentityLive
{
    [HarmonyPatch(typeof(StreamerMode), nameof(StreamerMode.SetHideIdentity))]
    internal static class HideIdentitySetPatch
    {
        private static bool Prefix(bool on)
        {
            if (!Engine.Enabled<HideIdentityLiveFeature>())
                return true;
            if (Managers.Network == null || !Managers.Network.IsInRoom)
                return true;

            PlayerPrefs.SetInt("SETTING_STREAMER_HIDE_ID", on ? 1 : 0);
            PlayerPrefs.Save();
            HideIdentitySync.Apply(on);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_Setting_ETC), nameof(UI_Setting_ETC.StartSection))]
    internal static class HideIdentitySettingUiPatch
    {
        private static void Postfix(UI_Setting_ETC __instance)
        {
            if (Engine.Enabled<HideIdentityLiveFeature>())
                HideIdentitySettingUi.Refresh(__instance);
        }
    }

    internal static class HideIdentitySettingUi
    {
        internal static void Refresh()
        {
            foreach (UI_Setting_ETC ui in UnityEngine.Object.FindObjectsByType<UI_Setting_ETC>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ui != null && ui.isActiveAndEnabled)
                    Refresh(ui);
            }
        }

        internal static void Refresh(UI_Setting_ETC ui)
        {
            try
            {
                Toggle toggle = Util.FindChild<Toggle>(ui.gameObject, "StreamerHideIdToggle", recursive: true);
                if (toggle != null)
                    toggle.interactable = Engine.Enabled<HideIdentityLiveFeature>() || StreamerMode.CanChange;
            }
            catch (Exception ex)
            {
                Log.Warn<HideIdentityLiveFeature>(ex, "同步隐藏身份开关状态失败");
            }
        }
    }
}
