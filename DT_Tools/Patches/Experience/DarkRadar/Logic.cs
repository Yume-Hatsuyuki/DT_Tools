using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Experience.DarkRadar
{
    /// <summary>巡检逻辑：按设备缓存实时状态同步两类 pin（新增出现的、移除消失的）。</summary>
    internal static class DarkRadarLogic
    {
        public static void SyncAll()
        {
            if (Time.time < DarkRadarState.NextCheck)
                return;
            DarkRadarState.NextCheck = Time.time + DarkRadarFeature.CheckInterval;

            MyPlayer my = Managers.Player?.MyPlayer;
            if (my == null || Managers.Map == null)
            {
                ClearAll();
                return;
            }

            // SendSabotageMission 只单播给 MasterMind(Dark)。
            // 给 White（好人）和 Black（好人拔刀后的持刀者）补建；Dark 原版已可见，不介入。
            if (my.Color == EPlayerColor.Dark)
            {
                ClearAll();
                return;
            }

            EGameState state = Managers.Game.State;
            if (state != EGameState.Survive && state != EGameState.Detective)
            {
                ClearAll();
                return;
            }

            if (Managers.Device == null || Managers.Device.Cache == null)
                return;

            // 黑幕收到 ScWeapon(39)→SabotageWeapon；数据源对齐：OpenArmory 的武器架
            Sync(Define.EMinimapPinType.SabotageWeapon,
                 Devices.OpenArmories().Select(d => d.Position),
                 DarkRadarState.WeaponPins);

            // 黑幕收到 ScFusebox(38)→SabotageFusebox；数据源对齐：已武装且完好的电闸
            Sync(Define.EMinimapPinType.SabotageFusebox,
                 Devices.ArmedIntactFuseboxes().Select(d => d.Position),
                 DarkRadarState.FuseboxPins);
        }

        /// <summary>按设备缓存的实时状态同步一类 pin；AddCommonPin 内部按 (type, pos) 去重。</summary>
        private static void Sync(Define.EMinimapPinType type,
                                 IEnumerable<Vector2> desiredPositions,
                                 HashSet<Vector2> mine)
        {
            // 入参恒为 Select() 迭代器（两个调用点均传 IEnumerable），as 分支不可达，直接落 HashSet
            var desired = new HashSet<Vector2>(desiredPositions);

            foreach (var pos in desired)
            {
                if (mine.Add(pos))
                    Managers.Map.AddCommonPin(type, pos);
            }

            foreach (var pos in mine.ToList())
            {
                if (!desired.Contains(pos))
                {
                    Managers.Map.RemoveCommonPin(type, pos);
                    mine.Remove(pos);
                }
            }
        }

        /// <summary>移除本功能添加的全部 pin（Enabled 关闭、离开对局、黑幕视角时调用）。</summary>
        public static void ClearAll()
        {
            if (Managers.Map == null)
            {
                DarkRadarState.Reset();
                return;
            }

            foreach (var pos in DarkRadarState.WeaponPins)
                Managers.Map.RemoveCommonPin(Define.EMinimapPinType.SabotageWeapon, pos);
            DarkRadarState.WeaponPins.Clear();

            foreach (var pos in DarkRadarState.FuseboxPins)
                Managers.Map.RemoveCommonPin(Define.EMinimapPinType.SabotageFusebox, pos);
            DarkRadarState.FuseboxPins.Clear();
        }
    }
}
