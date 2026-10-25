using System.Collections.Generic;
using System.Linq;
using Data;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Game
{
    public static class Devices
    {
        public const int FuseboxStateBroken = 9999;

        public const int FuseboxStateIntact = 0;

        public const int FuseboxMissionArmed = -1;

        public static List<DeviceBase> OpenArmories()
            => AllOf(EDeviceType.Armory)
                .Where(d => d.DeviceState == (int)EArmoryState.OpenArmory)
                .ToList();

        public static List<DeviceBase> AllOf(EDeviceType type)
            => Managers.Device.Cache.Values
                .Where(d => d != null && d.DeviceType == type)
                .OrderBy(d => d.ID)
                .ToList();

        public static List<DeviceBase> ArmedIntactFuseboxes()
            => AllOf(EDeviceType.Fusebox)
                .Where(f => f.Info != null
                            && f.Info.MissionType == FuseboxMissionArmed
                            && f.Info.StateList != null
                            && f.Info.StateList.Count > 0
                            && f.Info.StateList[0] == FuseboxStateIntact)
                .ToList();

        public static List<Server.Game.Storage> GetStorages(Server.Game.DeviceManager dm)
            => Traverse.Create(dm).Field("_storages").GetValue<List<Server.Game.Storage>>();
    }
}
