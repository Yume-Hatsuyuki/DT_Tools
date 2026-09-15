using System;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 扫描读条。用 CastingTime 替代调用方传入的 2f；配置 ≥0.5 时仍尊重 ScanUp→0.5。
    /// </summary>
    [HarmonyPatch(typeof(GameManagerEX), "StartScanning")]
    [PatchFeature(
        section: "StartScanning",
        description: "福尔摩斯：可修改搜索读条时长（默认 2.0s）。配置<0.5s 忽略 ScanUp。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class ScanTimeFeature
    {
        [ConfigField(2.0f, "扫描读条时长（秒）。建议 >= 0.1。")]
        public static ConfigEntry<float> CastingTime;

        [HarmonyPrefix]
        private static bool Prefix(GameManagerEX __instance, ref float castingTime, Action callback)
        {
            if (__instance.ScanningSlider != null)
                return false;

            float t = CastingTime.Value;
            if (t < 0.1f || float.IsNaN(t) || float.IsInfinity(t))
                t = 0.1f;

            // 原版：IsScanUp 时强制 0.5。配置更短时不套用，避免“加速变成减速”。
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
