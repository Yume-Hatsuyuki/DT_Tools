using System;
using System.Collections.Generic;
using DT_Tools.Core;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SitOnChairs
{
    internal static class LocalChairSpawn
    {
        private static readonly int[] ChairIds =
        {
            19901, 19902, 19903,
            19911, 19912, 19913, 19914, 19915, 19916, 19917, 19918
        };

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
                    info.StateList.Add(0);
                    dm.Spawn(info);
                    spawned++;
                }
                if (spawned > 0)
                {
                    Log.Info<SitOnChairsFeature>($"房主未装 mod，本地模拟补 spawn {spawned} 把椅子（本地可见可坐，装本 mod 的玩家经 SyncSitting 互见坐姿）。");
                }
            }
            catch (Exception ex)
            {
                Log.Error<SitOnChairsFeature>(ex, "本地补 spawn 椅子失败");
            }
        }
    }
}
