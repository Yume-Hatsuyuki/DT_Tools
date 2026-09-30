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
        /// <summary>
        /// 设备在当前档位下是否对本地玩家颜色放行（按设备实例判定：销毁证据目标是
        /// 实例属性 DeviceBase.IsDestroyEvidenceTarget（0.1.15b DeviceBase.cs:78），
        /// 黑方原版就可用，仅当白方档开启时才为白方放行）。
        /// </summary>
        public static bool ColorAllowedForDevice(DeviceBase device, EPlayerColor color)
        {
            if (color == EPlayerColor.Dark)
                return true;    // 原版本就放行，与档位无关
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
        /// 输入分发放行（Patch.Input 三处入口共用）：放行设备的 Special 键/杀按钮
        /// 优先触发破坏（与已亮出的提示一致），返回 true 表示已接管本次输入。
        /// 文案经 GetInteractSabotageMessageBase（显示层补丁已按档位接管：白方走
        /// EvaluateMessage、黑方走原版）——文案为空（如门已锁）时不接管，走原版行为。
        /// </summary>
        public static bool TryDispatchExtended(MyPlayer my)
        {
            if (my == null || my.Color == EPlayerColor.Dark)
                return false;    // 黑幕走原版链（含手武器优先判定）
            var dev = my.InteractDevice;
            if (dev == null || !ColorAllowedForDevice(dev, my.Color))
                return false;
            if (!Managers.Game.IsAlive || dev.InteractSabotageCooltime > 0)
                return false;    // 死亡/冷却中：走原版（提示栏此时显示倒计时）
            string msg = dev.GetInteractSabotageMessageBase(my.InteractDeviceIndex);
            if (string.IsNullOrEmpty(msg))
                return false;    // 无文案（门已锁/设备态不符）→ 原版行为
            dev.UseSabotageBase(my.InteractDeviceIndex);
            if (my.Color == EPlayerColor.White && dev.IsDestroyEvidenceTarget)
                RecordDestroyAttempt();   // 白方销毁证据依赖房主「WhiteSabotageClue」，未启用时 2 秒后提示
            return true;
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
            bool extended = ColorAllowedForDevice(dev, myPlayer.Color);
            if ((myPlayer.Color != EPlayerColor.Dark && !blackBase && !extended)
                || myPlayer.State == EPlayerState.Interact
                || myPlayer.State == EPlayerState.Casting)
            {
                return;
            }

            // 白方/黑方走档位放行时，消息取自本功能的 EvaluateMessage（其余走原版方法）
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

            const string hint = "锁门未生效：需房主启用「DoorLockServer」且档位（Mode）放行该身份";
            Log.Warn<SabotageButtonUnlockFeature>(hint);
            if (Managers.UI?.SceneUI is UI_GameScene scene)
                scene.ShowBroadcastNotice(hint);
        }

        // ── 销毁证据失败反馈（白方）：读条结束且发包后仍无 S_COOLTIME_DESTROY_EVIDENCE 回执即本地提示 ──

        private static float _lastDestroyAckRealtime = float.NegativeInfinity;

        /// <summary>白方经放行档位触发销毁证据时调用（Logic.TryDispatchExtended）。</summary>
        public static void RecordDestroyAttempt()
        {
            float attempt = Time.realtimeSinceStartup;
            CoroutineHost.Start(CoVerifyDestroy(attempt));
        }

        /// <summary>Handle_S_COOLTIME_DESTROY_EVIDENCE 回执时调用（黑白幕原版路径同样会回执，仅刷新时间戳）。</summary>
        public static void RecordDestroyAck()
        {
            _lastDestroyAckRealtime = Time.realtimeSinceStartup;
        }

        private static IEnumerator CoVerifyDestroy(float attemptRealtime)
        {
            yield return new WaitForSecondsRealtime(2f);
            // 销毁证据走 2.5 秒读条（DeviceBase.UseSabotage → StartCasting，0.1.15b DeviceBase.cs:457-467），
            // 读条结束（StopCasting 置空 CastingSlider，0.1.15b GameManagerEX.cs:782-786）后才发
            // C_DESTROY_EVIDENCE、服务端才可能回执——读条未结束不能判定，否则成功也在窗口内误报"未生效"
            while (Managers.Game.CastingSlider != null)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);    // 发包 + 服务端往返余量
            if (_lastDestroyAckRealtime >= attemptRealtime)
                yield break;    // 服务端受理

            const string hint = "销毁证据未生效：需房主启用「WhiteSabotageClue」放行白方";
            Log.Warn<SabotageButtonUnlockFeature>(hint);
            if (Managers.UI?.SceneUI is UI_GameScene scene)
                scene.ShowBroadcastNotice(hint);
        }
    }
}
