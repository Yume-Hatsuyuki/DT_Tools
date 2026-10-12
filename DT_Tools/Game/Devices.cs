using System.Collections.Generic;
using System.Linq;
using Data;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Game
{
    /// 设备访问助手：本机缓存查询（设备状态经 S_INIT_MAP / S_MODIFY_DEVICE 全员广播，
    /// 任何客户端的缓存都是实时的，数据源对齐无 Host 特权）+ 房主侧服务端货架读取
    /// （SupplyShelf / SupplyShelfRefill 共用，原 SupplyShelf 私有 Logic 上浮）。
    public static class Devices
    {
        /// 电闸损坏：StateList[0]==9999（0.1.17a Define.cs:554 DISCONNET_STATE；拉闸写入见 Server.Game/Fusebox.cs:122）。
        public const int FuseboxStateBroken = 9999;

        /// 电闸完好：StateList[0]==0（0.1.17a Define.cs:556 NORMAL_STATE）。
        public const int FuseboxStateIntact = 0;

        /// 破坏任务武装：MissionType==-1（开局 StartFuseboxSabotage → StartMission 写入，0.1.17a Server.Game/Fusebox.cs:141）。
        public const int FuseboxMissionArmed = -1;

        /// 当前处于 OpenArmory（可拔刀）的武器架。
        public static List<DeviceBase> OpenArmories()
            => AllOf(EDeviceType.Armory)
                .Where(d => d.DeviceState == (int)EArmoryState.OpenArmory)
                .ToList();

        public static List<DeviceBase> AllOf(EDeviceType type)
            => Managers.Device.Cache.Values
                .Where(d => d != null && d.DeviceType == type)
                .OrderBy(d => d.ID)
                .ToList();

        /// 已武装且完好的电闸（开局 StartFuseboxSabotage 随机武装 3 个）。
        public static List<DeviceBase> ArmedIntactFuseboxes()
            => AllOf(EDeviceType.Fusebox)
                .Where(f => f.Info != null
                            && f.Info.MissionType == FuseboxMissionArmed
                            && f.Info.StateList != null
                            && f.Info.StateList.Count > 0
                            && f.Info.StateList[0] == FuseboxStateIntact)
                .ToList();

        /// 服务端设备管理器的全部货架（私有字段 _storages，0.1.17a Server.Game/DeviceManager.cs:32）。房主侧功能用。
        public static List<Server.Game.Storage> GetStorages(Server.Game.DeviceManager dm)
            => Traverse.Create(dm).Field("_storages").GetValue<List<Server.Game.Storage>>();
    }
}
