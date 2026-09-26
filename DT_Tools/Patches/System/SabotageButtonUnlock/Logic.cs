using System;
using System.Collections;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>
    /// 显示层判定与交互提示整替：按档位扩展 DeviceBase.GetInteractSabotageMessageBase
    /// （0.1.15b DeviceBase.cs:389-403 的 Dark/Black 门槛）与
    /// UI_GameScene.ShowInteractSabotageText（0.1.15b UI_GameScene.cs:1355-1400 的 flag）
    /// 两层判定；ChatDevice/尸体/销毁证据目标维持原版 Black 分支不动。
    /// </summary>
    internal static class SabotageButtonUnlockLogic
    {
        /// <summary>设备类型在当前档位下是否对本地玩家颜色放行。</summary>
        public static bool ColorAllowedForDevice(EDeviceType type, EPlayerColor color)
        {
            if (color == EPlayerColor.Dark)
                return true;    // 原版本就放行，与档位无关
            if (type == EDeviceType.Door)
                return ModeAllows(SabotageButtonUnlockFeature.LockDoorMode, color);
            if (type == EDeviceType.Fusebox)
                return ModeAllows(SabotageButtonUnlockFeature.BreakPowerMode, color);
            return false;
        }

        public static bool ModeAllows(SabotageMode mode, EPlayerColor color)
        {
            if (color == EPlayerColor.Dark)
                return true;    // 黑幕原本可用，维持不变
            if (color == EPlayerColor.Black)
                return mode == SabotageMode.Black || mode == SabotageMode.All;
            if (color == EPlayerColor.White)
                return mode == SabotageMode.White || mode == SabotageMode.All;
            return false;
        }

        /// <summary>
        /// 档位放行路径下的消息求值（镜像原版 GetInteractSabotageMessageBase 通过
        /// 颜色门槛后的余下逻辑：存活 → 设备态 → 置键有效标志 → 取设备文案）。
        /// IsActiveInteractSabotageKey 为 protected set（DeviceBase.cs:62），经反射写入。
        /// </summary>
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

        /// <summary>
        /// UI_GameScene.ShowInteractSabotageText 整替（私有方法，字符串定位：
        /// 0.1.15b UI_GameScene.cs:1355-1400）：原版 flag 基础上按档位扩展
        /// Door/Fusebox 的放行分支，其余 UI 写入逐句保持原版。
        /// </summary>
        public static void RefreshSabotagePrompt(UI_GameScene scene)
        {
            var t = HarmonyLib.Traverse.Create(scene);
            var myPlayer = Managers.Player.MyPlayer;
            Go(scene, 22).SetVisible(visible: false, layoutIgnore: true);
            if (t.Field("_carryTrickPromptActive").GetValue<bool>())   // 私有字段：UI_GameScene.cs
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
            bool extended = ColorAllowedForDevice(dev.DeviceType, myPlayer.Color);
            if ((myPlayer.Color != EPlayerColor.Dark && !blackBase && !extended)
                || myPlayer.State == EPlayerState.Interact
                || myPlayer.State == EPlayerState.Casting)
            {
                return;
            }

            // 白板/黑方走档位放行时，消息取自本功能的 EvaluateMessage（其余走原版方法）
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

        // ── 锁门失败反馈（非黑幕）：2 秒内无 S_COOLTIME_SABOTAGE 回执即本地提示 ──

        private static float _lastAckRealtime = float.NegativeInfinity;

        /// <summary>Door.UseSabotage 发包时调用（非黑幕）。</summary>
        public static void RecordLockAttempt()
        {
            float attempt = Time.realtimeSinceStartup;
            CoroutineHost.Start(CoVerifyLock(attempt));
        }

        /// <summary>Handle_S_COOLTIME_SABOTAGE 回执时调用。</summary>
        public static void RecordLockAck()
        {
            _lastAckRealtime = Time.realtimeSinceStartup;
        }

        private static IEnumerator CoVerifyLock(float attemptRealtime)
        {
            yield return new WaitForSecondsRealtime(2f);
            if (_lastAckRealtime >= attemptRealtime)
                yield break;    // 服务端受理

            const string hint = "锁门未生效：需房主启用「DoorLockServer」服务端放行";
            Log.Warn<SabotageButtonUnlockFeature>(hint);
            if (Managers.UI?.SceneUI is UI_GameScene scene)
                scene.ShowBroadcastNotice(hint);
        }
    }
}
