using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.DarkRadar
{
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

            Sync(Define.EMinimapPinType.SabotageWeapon,
                 Devices.OpenArmories().Select(d => d.Position),
                 DarkRadarState.WeaponPins);

            Sync(Define.EMinimapPinType.SabotageFusebox,
                 Devices.ArmedIntactFuseboxes().Select(d => d.Position),
                 DarkRadarState.FuseboxPins);
        }

        private static void Sync(Define.EMinimapPinType type,
                                 IEnumerable<Vector2> desiredPositions,
                                 HashSet<Vector2> mine)
        {
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
