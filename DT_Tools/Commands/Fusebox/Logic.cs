using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.Fusebox
{
    /// <summary>
    /// 修电 / 拆电共用业务（对应旧 FuseboxHelper 的校验与协程发包；
    /// 设备查询已上浮 Game/Devices.cs，不在此重复）。
    ///
    /// 原版机制：
    ///   - 同一个 C_INTERACT_FUSEBOX 经 DeviceManager.Interact → Fusebox.Interact（0.1.15b
    ///     Server.Game/Fusebox.cs:16），按 StateList[0] 分流：0→DisconnetCable（拉闸，:114），
    ///     9999→ConnetCable（修电，:97）。
    ///   - DisconnetCable 仅校验 MissionType==-1（开局 StartFuseboxSabotage 武装的 3 个电闸），
    ///     无 Black/Dark 颜色校验、无距离、无拔螺栓谜题校验（好人也能拉闸）。
    ///   - ConnetCable 无任何身份/读条校验，直接恢复供电。
    ///   - 服务端 DeviceManager.Interact 有 500ms InteractLock 冷却（Player.InteractLock
    ///     setter 内 PushAfter(500) 延迟清除），连续发包需间隔 &gt;0.5s 避让。
    /// </summary>
    internal static class FuseboxLogic
    {
        /// <summary>服务端 InteractLock 冷却 500ms，发包间隔取 600ms 以确保不被吞包。</summary>
        public const float PacketInterval = 0.6f;

        /// <summary>电闸损坏状态值（StateList[0]==9999 表示已被拉断；数值唯一来源为 Game/Devices 常量）。</summary>
        public const int StateBroken = Devices.FuseboxStateBroken;

        /// <summary>电闸完好状态值（StateList[0]==0 表示正常/可被拉断）。</summary>
        public const int StateIntact = Devices.FuseboxStateIntact;

        /// <summary>破坏任务武装标记（MissionType==-1 表示该电闸已被选为破坏目标）。</summary>
        public const int MissionArmed = Devices.FuseboxMissionArmed;

        /// <summary>
        /// 校验本机已进入对局 + 已连上 Host + 处于生存阶段。
        /// 失败时输出提示并经 code 返回对应错误码（沿用旧实现）。
        /// 修电/拆电都只在 Survive 阶段生效。
        /// </summary>
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

        /// <summary>
        /// 收集已损坏电闸（StateList[0]==9999），可用于修电。
        /// （已武装完好的电闸查询走 Game/Devices.ArmedIntactFuseboxes()）
        /// </summary>
        public static List<DeviceBase> CollectBroken()
        {
            return Devices.AllOf(EDeviceType.Fusebox)
                .Where(f => f.Info != null
                            && f.Info.StateList != null
                            && f.Info.StateList.Count > 0
                            && f.Info.StateList[0] == StateBroken)
                .ToList();
        }

        /// <summary>按 ID 查找电闸。返回 (设备, 错误信息)；不存在时设备为 null。</summary>
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

        /// <summary>获取电闸状态中文标签。</summary>
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

        /// <summary>发送 C_INTERACT_FUSEBOX 到指定电闸 ID（StateList[0] 决定服务端分流方向）。</summary>
        public static void SendInteract(int fuseboxId)
        {
            Managers.Network.GameServer.Send(new C_INTERACT_FUSEBOX
            {
                FuseboxId = fuseboxId
            });
        }

        /// <summary>
        /// all 模式协程：逐个发 C_INTERACT_FUSEBOX，间隔 PacketInterval 秒避让服务端 InteractLock。
        /// 每次发包前校验网络链路和游戏阶段，对局结束 / 断线时中止。
        /// action 描述用于日志（"修电"/"拆电"）。
        /// </summary>
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

        /// <summary>all 模式启动协程（挂到 Core 常驻协程宿主，不依赖 WebConsole 组件存活）。</summary>
        public static bool StartPacketsCoroutine(List<int> ids, string action, CommandContext ctx)
        {
            Core.CoroutineHost.Start(SendPackets(ids, action, ctx));
            return true;
        }
    }
}
