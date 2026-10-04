using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.AutoFishMine
{
    /// <summary>
    /// 一键钓鱼挖矿的三个补丁（同一功能，三个入口）：
    /// 1) AutoStartPatch：DeviceManager.SearchInteractDevice Postfix——走近钓鱼点(EDeviceType.Fishing)
    ///    或矿机(EDeviceType.Miner)自动 InteractBase 开始（Fishing.Interact→StartFishing，
    ///    Mineral.Interact→StartCasting，见 0.1.16b Fishing.cs:45 / Mineral.cs:34）。
    /// 2) AutoFishingPatch：UI_FishingSlider.Update Postfix——钓鱼小游戏四阶段机（0.1.16b
    ///    UI_FishingSlider.cs:77-142）：Phase.Bite(1) 自动 EnterCatchIntro 提竿，
    ///    Phase.Reeling(3) 每帧 AddGauge（+0.05）模拟连按，gauge 满自动 EnterSuccess→收获。
    ///    其余阶段（Waiting/CatchIntro/Success）由游戏自动推进。
    /// 3) AutoMiningPatch：UI_MineralSlider.Update Postfix——滑块小游戏（0.1.16b UI_MineralSlider.cs:74）：
    ///    _isCatch 且 CheckCatch() 为 true（滑块进入目标区）时自动执行按键成功分支，
    ///    _successCount 累计 3 次后发 C_HANDLE_MINERAL(IsSuccess=true) 并 StopCasting 自动收获。
    /// 所有协议均为游戏原生请求，失败只记录日志，不中断。
    /// </summary>
    [HarmonyPatch(typeof(DeviceManager), nameof(DeviceManager.SearchInteractDevice))]
    internal static class AutoStartPatch
    {
        private static float _lastTriggerAt;

        private static void Postfix(MyPlayer player)
        {
            if (!Engine.Enabled<AutoFishMineFeature>() || !AutoFishMineFeature.AutoStart)
                return;
            if (player == null)
                return;
            if (Managers.Player == null || Managers.Player.MyPlayer != player)
                return;

            DeviceBase device = player.InteractDevice;
            if (device == null)
                return;
            if (device.DeviceType != EDeviceType.Fishing && device.DeviceType != EDeviceType.Miner)
                return;

            EPlayerState state = player.State;
            if (state == EPlayerState.Interact || state == EPlayerState.Casting
                || state == EPlayerState.Mining || state == EPlayerState.FishingState
                || state == EPlayerState.Hide || state == EPlayerState.Sit || state == EPlayerState.Carry)
                return;

            if (Managers.Game == null || Managers.Game.CastingSlider != null)
                return; // 正在读条，由游戏原生状态防重
            if (AutoFishMineFeature.Throttle > 0f
                && Time.unscaledTime - _lastTriggerAt < AutoFishMineFeature.Throttle)
                return;

            _lastTriggerAt = Time.unscaledTime;
            device.InteractBase(player.InteractDeviceIndex);
        }
    }

    /// <summary>自动钓鱼：咬钩自动提竿，收线自动连按。</summary>
    [HarmonyPatch(typeof(UI_FishingSlider), "Update")]
    internal static class AutoFishingPatch
    {
        private static void Postfix(UI_FishingSlider __instance)
        {
            if (!Engine.Enabled<AutoFishMineFeature>() || !AutoFishMineFeature.AutoFishing)
                return;

            int phase = GetPhase(__instance);
            if (phase == 1) // Phase.Bite：咬钩瞬间自动提竿
            {
                TryInvoke(__instance, "EnterCatchIntro"); // 0.1.16b UI_FishingSlider.cs:164
            }
            else if (phase == 3) // Phase.Reeling：自动连按收线
            {
                TryInvoke(__instance, "AddGauge"); // 0.1.16b UI_FishingSlider.cs:205
            }
        }

        private static int GetPhase(UI_FishingSlider popup)
        {
            try
            {
                return Convert.ToInt32(Traverse.Create(popup).Field("_phase").GetValue<object>()); // 0.1.16b UI_FishingSlider.cs:40
            }
            catch (Exception ex)
            {
                Log.Error<AutoFishMineFeature>($"读取钓鱼阶段失败：{ex.GetType().Name}: {ex.Message}");
                return -1;
            }
        }

        private static void TryInvoke(UI_FishingSlider popup, string methodName)
        {
            try
            {
                Traverse.Create(popup).Method(methodName).GetValue();
            }
            catch (Exception ex)
            {
                Log.Error<AutoFishMineFeature>($"{methodName} 调用失败：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>自动挖矿：滑块进入目标区自动连击，3 次成功自动收获。</summary>
    [HarmonyPatch(typeof(UI_MineralSlider), "Update")]
    internal static class AutoMiningPatch
    {
        private static void Postfix(UI_MineralSlider __instance)
        {
            if (!Engine.Enabled<AutoFishMineFeature>() || !AutoFishMineFeature.AutoMining)
                return;
            try
            {
                if (!Traverse.Create(__instance).Field("_isCatch").GetValue<bool>()) // 0.1.16b UI_MineralSlider.cs:28
                    return;
                if (!__instance.CheckCatch())
                    return; // 滑块未到位，等它摆回来

                // 滑块到位：执行与"按键成功"等价的分支（0.1.16b UI_MineralSlider.cs:81-100）
                Managers.Sound.PlaySystem("AxeSfx");
                Traverse.Create(__instance).Field("_mineral").GetValue<Mineral>()?.PlayAxeEffect(); // _mineral: 0.1.16b UI_MineralSlider.cs:22

                int count = Traverse.Create(__instance).Field("_successCount").GetValue<int>() + 1; // _successCount: 0.1.16b UI_MineralSlider.cs:24
                Traverse.Create(__instance).Field("_successCount").SetValue(count);
                if (count < 3)
                {
                    Traverse.Create(__instance).Method("Refresh").GetValue(); // 0.1.16b UI_MineralSlider.cs:65
                }
                else
                {
                    Managers.Network.GameServer.Send(new C_HANDLE_MINERAL
                    {
                        MineralId = __instance.DeviceID,
                        IsSuccess = true
                    });
                    Managers.Game.StopCasting();
                }
            }
            catch (Exception ex)
            {
                Log.Error<AutoFishMineFeature>($"自动挖矿失败：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
