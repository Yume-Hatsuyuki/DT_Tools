using System;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.AutoFishMine
{
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
                return;
            if (AutoFishMineFeature.Throttle > 0f
                && Time.unscaledTime - _lastTriggerAt < AutoFishMineFeature.Throttle)
                return;

            _lastTriggerAt = Time.unscaledTime;
            device.InteractBase(player.InteractDeviceIndex);
        }
    }

    [HarmonyPatch(typeof(UI_FishingSlider), "Update")]
    internal static class AutoFishingPatch
    {
        private static void Postfix(UI_FishingSlider __instance)
        {
            if (!Engine.Enabled<AutoFishMineFeature>() || !AutoFishMineFeature.AutoFishing)
                return;

            int phase = GetPhase(__instance);
            if (phase == 1)
            {
                TryInvoke(__instance, "EnterCatchIntro");
            }
            else if (phase == 3)
            {
                TryInvoke(__instance, "AddGauge");
            }
        }

        private static int GetPhase(UI_FishingSlider popup)
        {
            try
            {
                return Convert.ToInt32(Traverse.Create(popup).Field("_phase").GetValue<object>());
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

    [HarmonyPatch(typeof(UI_MineralSlider), "Update")]
    internal static class AutoMiningPatch
    {
        private static void Postfix(UI_MineralSlider __instance)
        {
            if (!Engine.Enabled<AutoFishMineFeature>() || !AutoFishMineFeature.AutoMining)
                return;
            try
            {
                if (!Traverse.Create(__instance).Field("_isCatch").GetValue<bool>())
                    return;
                if (!__instance.CheckCatch())
                    return;

                Managers.Sound.PlaySystem("AxeSfx");
                Traverse.Create(__instance).Field("_mineral").GetValue<Mineral>()?.PlayAxeEffect();

                int count = Traverse.Create(__instance).Field("_successCount").GetValue<int>() + 1;
                Traverse.Create(__instance).Field("_successCount").SetValue(count);
                if (count < 3)
                {
                    Traverse.Create(__instance).Method("Refresh").GetValue();
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
