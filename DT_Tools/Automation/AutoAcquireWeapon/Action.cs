using System;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoAcquireWeapon
{
    /// <summary>
    /// 动作：延迟等待 → 上限检查 → 持刀 / 颜色判定 → 选开放武器架 → 发送 C_INTERACT_ARMORY，
    /// 失败后按 RetryInterval 重试。发包路径与游戏一致：
    /// 白方 Armory.InteractArmory（0.1.15b Armory.cs:233）、黑方 UseSabotageArmory（0.1.15b Armory.cs:265）。
    /// </summary>
    internal static class AutoAcquireWeaponAction
    {
        public static void Run()
        {
            float delay = Mathf.Max(0f, AutoAcquireWeaponModule.DelaySeconds);
            if (AutoAcquireWeaponState.PhaseEnterRealtime < 0f)
                AutoAcquireWeaponState.PhaseEnterRealtime = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - AutoAcquireWeaponState.PhaseEnterRealtime < delay)
                return;
            if (Time.realtimeSinceStartup < AutoAcquireWeaponState.NextTryRealtime)
                return;

            int max = Math.Max(1, AutoAcquireWeaponModule.MaxAttempts);
            if (AutoAcquireWeaponState.Attempts >= max)
            {
                AutoAcquireWeaponState.MarkLeft();
                Log.Warn<AutoAcquireWeaponModule>($"已达 MaxAttempts={max}，本局停止自动取刀");
                return;
            }

            var my = Managers.Player?.MyPlayer;
            if (my == null || my.PrivateInfo == null)
            {
                ScheduleRetry(0.5f);
                return;
            }

            // 已持刀 / 已是黑方：无需再取
            if (my.Color == EPlayerColor.Black
                || (my.Inventory != null && my.Inventory.Weapon.DataId != 0))
            {
                AutoAcquireWeaponState.MarkLeft();
                Log.Info<AutoAcquireWeaponModule>($"已持刀或已是 Black（Color={my.Color}），跳过");
                return;
            }

            EArmoryInteractType interactType;
            switch (my.Color)
            {
                case EPlayerColor.White:
                    interactType = EArmoryInteractType.AcquireWeapon;
                    break;
                case EPlayerColor.Dark:
                    if (Managers.Game != null && Managers.Game.WeaponPickupLocked)
                    {
                        Log.Warn<AutoAcquireWeaponModule>("WeaponPickupLocked，暂缓截刀");
                        ScheduleRetry();
                        return;
                    }
                    interactType = EArmoryInteractType.SelectBlack;
                    break;
                default:
                    AutoAcquireWeaponState.MarkLeft();
                    Log.Warn<AutoAcquireWeaponModule>($"颜色 {my.Color} 不可取刀，停止");
                    return;
            }

            var open = Managers.Device?.Cache?.Values
                .Where(d => d != null
                            && d.DeviceType == EDeviceType.Armory
                            && d.DeviceState == (int)EArmoryState.OpenArmory)
                .OrderBy(d => d.ID)
                .ToList();

            if (open == null || open.Count == 0)
            {
                if (AutoAcquireWeaponState.Attempts == 0 || AutoAcquireWeaponState.Attempts % 5 == 0)
                    Log.Info<AutoAcquireWeaponModule>("尚无开放武器架，等待刷新…");
                ScheduleRetry();
                return;
            }

            var armory = open[0];
            AutoAcquireWeaponState.Attempts++;
            if (!ClientPackets.TrySend(new C_INTERACT_ARMORY
            {
                ArmoryId = armory.ID,
                Type = interactType
            }, out string error))
            {
                Log.Warn<AutoAcquireWeaponModule>($"取刀发包失败: {error}");
                ScheduleRetry();
                return;
            }

            Log.Info<AutoAcquireWeaponModule>(
                $"已发送 C_INTERACT_ARMORY armory=#{armory.ID} type={interactType}" +
                $"（尝试 {AutoAcquireWeaponState.Attempts}/{max}）");
            // 不立刻 done：下一拍检查是否已持刀；短暂间隔后再试
            AutoAcquireWeaponState.NextTryRealtime = Time.realtimeSinceStartup + 0.8f;
        }

        /// <summary>安排下一次重试时刻并计入一次尝试（与旧实现一致：失败也消耗尝试次数）。</summary>
        private static void ScheduleRetry(float? overrideInterval = null)
        {
            float interval = overrideInterval
                ?? Mathf.Max(0.2f, AutoAcquireWeaponModule.RetryInterval);
            AutoAcquireWeaponState.NextTryRealtime = Time.realtimeSinceStartup + interval;
            AutoAcquireWeaponState.Attempts++;
        }
    }
}
