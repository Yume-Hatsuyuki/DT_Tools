using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.ScanTime
{
    /// <summary>
    /// StartScanning 整替：复刻 0.1.15b GameManagerEX.cs:803，仅读条时长可配置。
    /// 公共方法（nameof 可定位，此处经 nameof）。原版 ScanUp 读 Manager 单例
    /// （Managers.Game.IsScanUp，0.1.15b GameManagerEX.cs:807），__instance 即该单例；
    /// ScanningSlider 属性为 private set（:256），经 Traverse 回写。
    /// UI_ScanningSlider.SetInfo：0.1.15b UI_ScanningSlider.cs:26；
    /// MakeWorldSpaceUI：0.1.15b UIManager.cs:754。
    /// </summary>
    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.StartScanning))]
    internal static class ScanTimePatch
    {
        private static bool Prefix(GameManagerEX __instance, ref float castingTime, Action callback)
        {
            if (!Engine.Enabled<ScanTimeFeature>())
                return true;

            if (__instance.ScanningSlider != null)
                return false;

            float t = ScanTimeFeature.CastingTime;
            if (t < 0.1f || float.IsNaN(t) || float.IsInfinity(t))
                t = 0.1f;

            // 原版：IsScanUp 时强制 0.5（0.1.15b GameManagerEX.cs:807）。
            // 配置更短时不套用，避免“加速变成减速”。
            if (t >= 0.5f && __instance.IsScanUp)
                t = 0.5f;

            castingTime = t;
            Managers.Sound.PlayLoop("ScanningSfx");
            MyPlayer my = Managers.Player.MyPlayer;
            if (__instance.IsAlive)
                my.ChangeMyPlayerState(EPlayerState.Scanning);

            UI_ScanningSlider slider = Managers.UI.MakeWorldSpaceUI<UI_ScanningSlider>(my.UIGroup);
            slider.SetInfo(castingTime, callback);
            slider.transform.localPosition = new Vector2(0f, -30f);
            Traverse.Create(__instance).Property("ScanningSlider").SetValue(slider);
            return false;
        }
    }
}
