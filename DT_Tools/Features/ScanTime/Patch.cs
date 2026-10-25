using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.ScanTime
{
    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.StartScanning))]
    internal static class ScanTimePatch
    {
        private static bool Prefix(GameManagerEX __instance, float castingTime, Action callback)
        {
            if (!Engine.Enabled<ScanTimeFeature>())
                return true;

            if (__instance.ScanningSlider != null)
                return false;

            float t = Game.Casting.ClampSeconds(ScanTimeFeature.CastingTime, 0.1f, 0.1f);

            if (t >= 0.5f && __instance.IsScanUp)
                t = 0.5f;

            Managers.Sound.PlayLoop("ScanningSfx");
            MyPlayer my = Managers.Player.MyPlayer;
            if (__instance.IsAlive)
                my.ChangeMyPlayerState(EPlayerState.Scanning);

            UI_ScanningSlider slider = Managers.UI.MakeWorldSpaceUI<UI_ScanningSlider>(my.UIGroup);
            slider.SetInfo(t, callback);
            slider.transform.localPosition = new Vector2(0f, -30f);
            Traverse.Create(__instance).Property("ScanningSlider").SetValue(slider);
            return false;
        }
    }
}
