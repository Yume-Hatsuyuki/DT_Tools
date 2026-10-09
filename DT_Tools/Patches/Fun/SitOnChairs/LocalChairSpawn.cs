using System;
using System.Collections.Generic;
using DT_Tools.Core;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.Fun.SitOnChairs
{
    /// <summary>
    /// 本地模拟椅子（非房主专用补丁）：椅子设备由房主服务端在开局时生成并下发
    /// （0.1.16b Server.Game/GameRoom.cs:679 InitDevices + DeviceManager.HandleAddDevice，
    /// 客户端本地注入的岛屿数据只是"模板"，真正的设备实例必须来自服务端）。
    /// 因此房主未装本 mod 时，非房主客户端本地没有任何椅子设备——看不到交互提示、无法坐下。
    /// 本模块在检测到"对局内设备缓存中没有椅子"时，把 11 把椅子（长椅×2、沙发×1、咖啡椅×8）
    /// 以本地注入数据（Managers.Data.DeviceDic 的 DataId 19901-19918）直接 spawn 进本地
    /// DeviceManager.Cache：本地立即出现椅子、显示"坐下"提示、按 E 本地坐（GameSit）；
    /// 装本 mod 的客户端各自按相同 DeviceId 本地 spawn（坐标一致），配合 SyncSitting
    /// 聊天广播即可互见坐姿；未装 mod 的客户端没有任何椅子（服务端不认这些设备）。
    /// 房主装了 mod 时服务端已下发椅子（Cache 有 Chair），本模块自动跳过，不重复。
    /// </summary>
    internal static class LocalChairSpawn
    {
        /// <summary>11 把椅子的数据 ID（与 DeviceData_Island.json 的 DataId 一致，也是 DeviceId）。</summary>
        private static readonly int[] ChairIds =
        {
            19901, 19902, 19903,
            19911, 19912, 19913, 19914, 19915, 19916, 19917, 19918
        };

        /// <summary>检查并补 spawn：对局内设备缓存没有椅子时，用本地注入数据把椅子加进 DeviceManager.Cache。</summary>
        internal static void Check()
        {
            try
            {
                if (!Engine.Enabled<SitOnChairsFeature>())
                {
                    return;
                }
                DeviceManager dm = Managers.Device;
                if (dm == null || dm.Cache == null || Managers.Data == null || Managers.Data.DeviceDic == null)
                {
                    return;
                }
                var game = Managers.Game;
                if (game == null || (game.State != EGameState.Survive && game.State != EGameState.Detective))
                {
                    return;
                }
                // 房主装了 mod（服务端已下发椅子）→ 有椅子就不补，避免重复 spawn
                foreach (DeviceBase device in dm.Cache.Values)
                {
                    if (device != null && device is Chair)
                    {
                        return;
                    }
                }
                int spawned = 0;
                foreach (int dataId in ChairIds)
                {
                    if (!Managers.Data.DeviceDic.TryGetValue(dataId, out Data.DeviceData data) || data == null)
                    {
                        continue;
                    }
                    ChairFactory.Ensure();
                    var info = new DeviceInfo
                    {
                        Type = EDeviceType.Chair,
                        DeviceId = dataId,
                        Pos = new PosInfo { X = data.Pos.X, Y = data.Pos.Y },
                        Bubble = 0,
                        MissionType = 0,
                        IsInfected = false
                    };
                    info.StateList.Add(0); // 0 = 可坐（空闲）
                    dm.Spawn(info);
                    spawned++;
                }
                if (spawned > 0)
                {
                    Log.Info<SitOnChairsFeature>($"[可坐椅子] 房主未装 mod，本地模拟补 spawn {spawned} 把椅子（本地可见可坐，装本 mod 的玩家经 SyncSitting 互见坐姿）。");
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>("[可坐椅子] 本地补 spawn 椅子失败：" + ex.Message);
            }
        }
    }
}
