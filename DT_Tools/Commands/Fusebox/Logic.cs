using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.Fusebox
{
    internal static class FuseboxLogic
    {
        public const float PacketInterval = 0.6f;

        public const int StateBroken = Devices.FuseboxStateBroken;

        public const int StateIntact = Devices.FuseboxStateIntact;

        public const int MissionArmed = Devices.FuseboxMissionArmed;

        public static bool ValidateClient(CommandContext ctx, out string code)
        {
            if (!LocalPlayer.TryGetPlayer(out _, out string localError))
            {
                ctx.Reply(localError);
                code = "not in game";
                return false;
            }
            if (!LocalPlayer.IsSurvive)
            {
                ctx.Reply($"仅生存阶段可用，当前状态: {LocalPlayer.StateText}。");
                code = "invalid state";
                return false;
            }
            if (Managers.Device == null || Managers.Device.Cache == null)
            {
                ctx.Reply("设备管理器尚未初始化。");
                code = "device manager not ready";
                return false;
            }
            code = null;
            return true;
        }

        public static List<DeviceBase> CollectBroken()
        {
            return Devices.AllOf(EDeviceType.Fusebox)
                .Where(f => f.Info != null
                            && f.Info.StateList != null
                            && f.Info.StateList.Count > 0
                            && f.Info.StateList[0] == StateBroken)
                .ToList();
        }

        public static DeviceBase FindFusebox(int id, out string error)
        {
            if (Managers.Device.Cache.TryGetValue(id, out var dev))
            {
                if (dev != null && dev.DeviceType == EDeviceType.Fusebox)
                {
                    error = null;
                    return dev;
                }
                error = $"设备 #{id} 不是电闸（{dev?.DeviceType ?? EDeviceType.NoneDevice}）。";
                return null;
            }
            error = $"找不到设备 #{id}。";
            return null;
        }

        public static string GetStateLabel(DeviceBase fusebox)
        {
            var info = fusebox?.Info;
            if (info == null || info.StateList == null || info.StateList.Count == 0)
                return "未知";
            int state = info.StateList[0];
            if (state == StateBroken) return "损坏";
            if (state == StateIntact)
                return info.MissionType == MissionArmed ? "完好(已武装)" : "完好";
            return $"状态{state}";
        }

        public static void SendInteract(int fuseboxId)
        {
            Managers.Network.GameServer.Send(new C_INTERACT_FUSEBOX
            {
                FuseboxId = fuseboxId
            });
        }

        public static IEnumerator SendPackets(List<int> ids, string action, CommandContext ctx)
        {
            int sent = 0;
            foreach (int id in ids)
            {
                if (Managers.Network == null || Managers.Network.GameServer == null)
                {
                    ctx.Warn($"【{action}】网络链路已断开，中止剩余发包。");
                    yield break;
                }
                if (!LocalPlayer.IsSurvive)
                {
                    ctx.Warn($"【{action}】已离开生存阶段，中止剩余发包。");
                    yield break;
                }

                SendInteract(id);
                sent++;
                ctx.Reply($"【{action}】已发送 {sent}/{ids.Count}：电闸 #{id}");

                if (sent < ids.Count)
                    yield return new WaitForSeconds(PacketInterval);
            }
            ctx.Reply($"【{action}】全部完成：已发送 {sent}/{ids.Count} 个 C_INTERACT_FUSEBOX。");
        }

        public static bool StartPacketsCoroutine(List<int> ids, string action, CommandContext ctx)
        {
            Core.CoroutineHost.Start(SendPackets(ids, action, ctx));
            return true;
        }
    }
}
