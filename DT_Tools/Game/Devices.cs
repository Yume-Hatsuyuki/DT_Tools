using System.Collections.Generic;
using System.Linq;
using Data;
using Protocol;

namespace DT_Tools.Game
{
    /// <summary>
    /// 本机设备缓存查询。设备状态经 S_INIT_MAP / S_MODIFY_DEVICE 全员广播，
    /// 任何客户端的缓存都是实时的（数据源对齐，无 Host 特权）。
    /// </summary>
    public static class Devices
    {
        /// <summary>电闸损坏：StateList[0]==9999（0.1.15b Define.cs:562 DISCONNET_STATE；拉闸写入见 Server.Game/Fusebox.cs:122）。</summary>
        public const int FuseboxStateBroken = 9999;

        /// <summary>电闸完好：StateList[0]==0（0.1.15b Define.cs:564 NORMAL_STATE）。</summary>
        public const int FuseboxStateIntact = 0;

        /// <summary>破坏任务武装：MissionType==-1（开局 StartFuseboxSabotage → StartMission 写入，0.1.15b Server.Game/Fusebox.cs:141）。</summary>
        public const int FuseboxMissionArmed = -1;

        /// <summary>当前处于 OpenArmory（可拔刀）的武器架。</summary>
        public static List<DeviceBase> OpenArmories()
            => AllOf(EDeviceType.Armory)
                .Where(d => d.DeviceState == (int)EArmoryState.OpenArmory)
                .ToList();

        public static List<DeviceBase> AllOf(EDeviceType type)
            => Managers.Device.Cache.Values
                .Where(d => d != null && d.DeviceType == type)
                .OrderBy(d => d.ID)
                .ToList();

        /// <summary>已武装且完好的电闸（开局 StartFuseboxSabotage 随机武装 3 个）。</summary>
        public static List<DeviceBase> ArmedIntactFuseboxes()
            => AllOf(EDeviceType.Fusebox)
                .Where(f => f.Info != null
                            && f.Info.MissionType == FuseboxMissionArmed
                            && f.Info.StateList != null
                            && f.Info.StateList.Count > 0
                            && f.Info.StateList[0] == FuseboxStateIntact)
                .ToList();
    }
}
