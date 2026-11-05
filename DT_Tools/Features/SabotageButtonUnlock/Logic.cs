using System;
using System.Collections;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    internal static class SabotageButtonUnlockLogic
    {
        // 与服务端 DoorLockServer 的自动解锁时长一致，仅用于本地剩余时间提示。
        private const float LockSeconds = 20f;

        private static readonly Dictionary<int, float> _localLockExpiry = new Dictionary<int, float>();

        private static int _cooltimeStartSurvivalTime;
        private static int _cooltimeEndSurvivalTime;

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
            return AccessTools.Method(typeof(DeviceBase), "GetInteractSabotageMessage")
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
            var myPlayer = Managers.Player?.MyPlayer;
            if (myPlayer == null || myPlayer.InteractDevice == null)
                return;
            if (myPlayer.State == EPlayerState.Interact || myPlayer.State == EPlayerState.Casting)
                return;

            var t = HarmonyLib.Traverse.Create(scene);
            if (t.Field("_carryTrickPromptActive").GetValue<bool>() || myPlayer.CanHandWeapon)
                return;
            if (myPlayer.Color == EPlayerColor.Dark)
                return;

            var dev = myPlayer.InteractDevice;
            bool blackBase = myPlayer.Color == EPlayerColor.Black
                && (dev.DeviceType == EDeviceType.ChatDevice
                    || dev.DeviceType == EDeviceType.Corpse
                    || dev.IsDestroyEvidenceTarget);
            if (blackBase || !ColorAllowedForDevice(dev, myPlayer.Color))
                return;

            string message = EvaluateMessage(dev, myPlayer.InteractDeviceIndex);
            if (string.IsNullOrEmpty(message))
                return;

            Go(scene, 21).SetVisible(true);
            Txt(scene, 20).text = message;
            int cooltime = RemainingCooltime();
            Txt(scene, 20).color = cooltime > 0 ? Color.gray : Color.white;
            Txt(scene, 9).text = cooltime > 0
                ? cooltime.ToString()
                : KeyBindings.Label(Define.EInputAction.Special);
            bool isActive = dev.IsActiveInteractSabotageKey;
            Go(scene, 22).SetVisible(isActive, !isActive);
            scene.gameObject.RebuildLayout();
        }

        private static GameObject Go(UI_GameScene scene, int index)
            => AccessTools.Method(typeof(UI_Base), "GetObject")?.Invoke(scene, new object[] { index }) as GameObject;

        private static TMPro.TMP_Text Txt(UI_GameScene scene, int index)
            => AccessTools.Method(typeof(UI_Base), "GetText")?.Invoke(scene, new object[] { index }) as TMPro.TMP_Text;

        public static void RecordLockAttempt(Door door)
        {
            if (door == null || door.DoorData == null)
                return;
            float attempt = Time.realtimeSinceStartup;
            _localLockExpiry[door.DoorData.DataId] = attempt + LockSeconds;
            CoroutineHost.Start(CoVerifyLock(door));
        }

        public static int RemainingLockSeconds(Door door)
        {
            if (door == null || door.DoorData == null)
                return 0;
            int doorId = door.DoorData.DataId;
            float left = _localLockExpiry.TryGetValue(doorId, out float expiry)
                ? expiry - Time.realtimeSinceStartup
                : 0f;
            if (left <= 0f || door.DeviceState != 2)
            {
                _localLockExpiry.Remove(doorId);
                return 0;
            }
            return Mathf.CeilToInt(left);
        }

        // 每玩家全局冷却，与 c 版同语义：只在本机生存阶段推进，新一局归零。
        public static int RemainingCooltime()
        {
            if (SabotageButtonUnlockFeature.LockCooltime <= 0 || _cooltimeEndSurvivalTime <= 0)
                return 0;
            if (Managers.Game == null)
                return 0;
            int now = Managers.Game.SurvivalTime;
            if (now < _cooltimeStartSurvivalTime)
            {
                ResetCooltime();
                return 0;
            }
            int left = _cooltimeEndSurvivalTime - now;
            if (left <= 0)
            {
                ResetCooltime();
                return 0;
            }
            return left;
        }

        private static void BeginCooltime()
        {
            if (SabotageButtonUnlockFeature.LockCooltime <= 0 || Managers.Game == null)
                return;
            _cooltimeStartSurvivalTime = Managers.Game.SurvivalTime;
            _cooltimeEndSurvivalTime = _cooltimeStartSurvivalTime + SabotageButtonUnlockFeature.LockCooltime;
        }

        private static void ResetCooltime()
        {
            _cooltimeStartSurvivalTime = 0;
            _cooltimeEndSurvivalTime = 0;
        }

        private static IEnumerator CoVerifyLock(Door door)
        {
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                if (door != null && door.DeviceState == 2)
                {
                    BeginCooltime();
                    yield break;
                }
            }

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
