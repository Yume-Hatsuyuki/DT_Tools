using System;
using System.Collections;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    internal static class SabotageButtonUnlockLogic
    {
        public static bool ColorAllowedForDevice(DeviceBase device, EPlayerColor color)
        {
            if (color == EPlayerColor.Dark)
                return true;
            if (device.IsDestroyEvidenceTarget)
                return color == EPlayerColor.White && SabotageButtonUnlockFeature.WhiteDestroyEvidence;
            if (device.DeviceType == EDeviceType.Door)
                return ModeAllows(SabotageButtonUnlockFeature.LockDoorMode, color);
            if (device.DeviceType == EDeviceType.Fusebox)
                return ModeAllows(SabotageButtonUnlockFeature.BreakPowerMode, color);
            return false;
        }

        public static bool ModeAllows(SabotageMode mode, EPlayerColor color)
        {
            if (color == EPlayerColor.Dark)
                return true;
            if (color == EPlayerColor.Black)
                return mode == SabotageMode.Black || mode == SabotageMode.All;
            if (color == EPlayerColor.White)
                return mode == SabotageMode.White || mode == SabotageMode.All;
            return false;
        }

        public static string EvaluateMessage(DeviceBase device, int index)
        {
            if (!Managers.Game.IsAlive)
                return "";
            var isState = AccessTools.Method(typeof(DeviceBase), "IsInteractSabotageState");
            if (!(bool)(isState?.Invoke(device, null) ?? false))
                return "";
            AccessTools.Property(typeof(DeviceBase), nameof(DeviceBase.IsActiveInteractSabotageKey))
                ?.SetValue(device, true);
            return AccessTools.Method(device.GetType(), "GetInteractSabotageMessage")
                ?.Invoke(device, new object[] { index }) as string ?? "";
        }

        public static bool TryDispatchExtended(MyPlayer my)
        {
            if (my == null || my.Color == EPlayerColor.Dark)
                return false;
            var dev = my.InteractDevice;
            if (dev == null || !ColorAllowedForDevice(dev, my.Color))
                return false;
            if (!Managers.Game.IsAlive || dev.InteractSabotageCooltime > 0)
                return false;
            string msg = dev.GetInteractSabotageMessageBase(my.InteractDeviceIndex);
            if (string.IsNullOrEmpty(msg))
                return false;
            dev.UseSabotageBase(my.InteractDeviceIndex);
            if (my.Color == EPlayerColor.White && dev.IsDestroyEvidenceTarget)
                RecordDestroyAttempt();
            return true;
        }

        public static void RefreshSabotagePrompt(UI_GameScene scene)
        {
            var t = HarmonyLib.Traverse.Create(scene);
            var myPlayer = Managers.Player.MyPlayer;
            Go(scene, 22).SetVisible(visible: false, layoutIgnore: true);
            if (t.Field("_carryTrickPromptActive").GetValue<bool>())
            {
                Go(scene, 22).SetVisible(true);
                Txt(scene, 20).text = Managers.GetText("DeadlyTrickInteract");
                Txt(scene, 20).color = Color.white;
                Txt(scene, 9).text = KeyBindings.Label(Define.EInputAction.Special);
                Go(scene, 23).SetVisible(true);
                scene.gameObject.RebuildLayout();
                return;
            }

            if (myPlayer.InteractDevice == null)
                return;
            var dev = myPlayer.InteractDevice;
            bool blackBase = myPlayer.Color == EPlayerColor.Black
                && (dev.DeviceType == EDeviceType.ChatDevice
                    || dev.DeviceType == EDeviceType.Corpse
                    || dev.IsDestroyEvidenceTarget);
            bool extended = ColorAllowedForDevice(dev, myPlayer.Color);
            if ((myPlayer.Color != EPlayerColor.Dark && !blackBase && !extended)
                || myPlayer.State == EPlayerState.Interact
                || myPlayer.State == EPlayerState.Casting)
            {
                return;
            }

            string message = extended && myPlayer.Color != EPlayerColor.Dark && !blackBase
                ? EvaluateMessage(dev, myPlayer.InteractDeviceIndex)
                : dev.GetInteractSabotageMessageBase(myPlayer.InteractDeviceIndex);
            if (!string.IsNullOrEmpty(message))
            {
                Go(scene, 22).SetVisible(true);
                Txt(scene, 20).text = message;
                int cooltime = dev.InteractSabotageCooltime;
                if (cooltime > 0)
                {
                    Txt(scene, 9).text = cooltime.ToString();
                    Txt(scene, 20).color = Color.gray;
                }
                else
                {
                    Txt(scene, 9).text = KeyBindings.Label(Define.EInputAction.Special);
                    Txt(scene, 20).color = Color.white;
                }
                bool isActive = dev.IsActiveInteractSabotageKey;
                Go(scene, 23).SetVisible(isActive, !isActive);
            }
            scene.gameObject.RebuildLayout();
        }

        private static GameObject Go(UI_GameScene scene, int index)
            => AccessTools.Method(typeof(UI_Base), "GetObject")?.Invoke(scene, new object[] { index }) as GameObject;

        private static TMPro.TMP_Text Txt(UI_GameScene scene, int index)
            => AccessTools.Method(typeof(UI_Base), "GetText")?.Invoke(scene, new object[] { index }) as TMPro.TMP_Text;

        private static float _lastAckRealtime = float.NegativeInfinity;

        public static void RecordLockAttempt()
        {
            float attempt = Time.realtimeSinceStartup;
            CoroutineHost.Start(CoVerifyLock(attempt));
        }

        public static void RecordLockAck()
        {
            _lastAckRealtime = Time.realtimeSinceStartup;
        }

        private static IEnumerator CoVerifyLock(float attemptRealtime)
        {
            yield return new WaitForSecondsRealtime(2f);
            if (_lastAckRealtime >= attemptRealtime)
                yield break;

            const string hint = "锁门未生效：需房主启用「DoorLockServer」且档位（Mode）放行该身份";
            Log.Warn<SabotageButtonUnlockFeature>(hint);
            if (Managers.UI?.SceneUI is UI_GameScene scene)
                scene.ShowBroadcastNotice(hint);
        }

        private static float _lastDestroyAckRealtime = float.NegativeInfinity;

        public static void RecordDestroyAttempt()
        {
            float attempt = Time.realtimeSinceStartup;
            CoroutineHost.Start(CoVerifyDestroy(attempt));
        }

        public static void RecordDestroyAck()
        {
            _lastDestroyAckRealtime = Time.realtimeSinceStartup;
        }

        private static IEnumerator CoVerifyDestroy(float attemptRealtime)
        {
            yield return new WaitForSecondsRealtime(2f);
            while (Managers.Game.CastingSlider != null)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);
            if (_lastDestroyAckRealtime >= attemptRealtime)
                yield break;

            const string hint = "销毁证据未生效：需房主启用「WhiteSabotageClue」放行白方";
            Log.Warn<SabotageButtonUnlockFeature>(hint);
            if (Managers.UI?.SceneUI is UI_GameScene scene)
                scene.ShowBroadcastNotice(hint);
        }
    }
}
