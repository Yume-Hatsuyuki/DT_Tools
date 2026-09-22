using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Data;
using Protocol;
using DT_Tools.Console.Commands.Weapon;

namespace DT_Tools.Console.Commands.Fusebox
{
    /// <summary>
    /// 修电 / 拆电共用：本机客户端身份校验、电闸状态查询、协程发包。
    ///
    /// 原版机制（0.1.14b）：
    ///   - 同一个 C_INTERACT_FUSEBOX 经 DeviceManager.Interact → Fusebox.Interact，
    ///     按 StateList[0] 分流：0→DisconnetCable（拉闸），9999→ConnetCable（修电）。
    ///   - DisconnetCable 仅校验 MissionType==-1（开局 StartFuseboxSabotage 武装的 3 个电闸），
    ///     无 Black/Dark 颜色校验、无距离、无拔螺栓谜题校验（好人也能拉闸）。
    ///   - ConnetCable 无任何身份/读条校验，直接恢复供电。
    ///   - 服务端 DeviceManager.Interact 有 500ms InteractLock 冷却（Player.InteractLock
    ///     setter 内 PushAfter(500) 延迟清除），连续发包需间隔 >0.5s 避让。
    /// </summary>
    internal static class FuseboxHelper
    {
        /// <summary>服务端 InteractLock 冷却 500ms，发包间隔取 600ms 以确保不被吞包。</summary>
        public const float PacketInterval = 0.6f;

        /// <summary>电闸损坏状态值（StateList[0]==9999 表示已被拉断）。</summary>
        public const int StateBroken = 9999;

        /// <summary>电闸完好状态值（StateList[0]==0 表示正常/可被拉断）。</summary>
        public const int StateIntact = 0;

        /// <summary>破坏任务武装标记（MissionType==-1 表示该电闸已被选为破坏目标）。</summary>
        public const int MissionArmed = -1;

        /// <summary>
        /// 校验本机已进入对局 + 已连上 Host + 处于生存阶段。
        /// 失败时输出提示并返回 false。修电/拆电都只在 Survive 阶段生效。
        /// </summary>
        public static bool ValidateClient(WebConsole console)
        {
            if (WeaponPacketHelper.RequireLocalPlayer(console) == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return false;
            }
            if (!WeaponPacketHelper.RequireSurvive(console))
            {
                console.SetResult("{\"ok\":false,\"error\":\"invalid state\"}");
                return false;
            }
            if (Managers.Device == null || Managers.Device.Cache == null)
            {
                console.Log("设备管理器尚未初始化。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"device manager not ready\"}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 从本机设备缓存中收集所有电闸（DeviceType==Fusebox），按 ID 排序。
        /// 电闸在 S_INIT_MAP 时随房间数据生成，全程常驻 Cache。
        /// </summary>
        public static List<DeviceBase> GetAllFuseboxes()
        {
            return Managers.Device.Cache.Values
                .Where(d => d != null && d.DeviceType == EDeviceType.Fusebox)
                .OrderBy(d => d.ID)
                .ToList();
        }

        /// <summary>
        /// 收集已损坏电闸（StateList[0]==9999），可用于修电。
        /// </summary>
        public static List<DeviceBase> CollectBroken()
        {
            return GetAllFuseboxes()
                .Where(f => f.Info != null
                            && f.Info.StateList != null
                            && f.Info.StateList.Count > 0
                            && f.Info.StateList[0] == StateBroken)
                .ToList();
        }

        /// <summary>
        /// 收集已武装且完好的电闸（MissionType==-1 && StateList[0]==0），可用于拆电。
        /// 开局 StartFuseboxSabotage 随机武装 3 个；第 2 个被拉断后 ClearFuseboxSabotage
        /// 会解除剩余电闸的武装（MissionType 置 0）。
        /// </summary>
        public static List<DeviceBase> CollectArmedIntact()
        {
            return GetAllFuseboxes()
                .Where(f => f.Info != null
                            && f.Info.MissionType == MissionArmed
                            && f.Info.StateList != null
                            && f.Info.StateList.Count > 0
                            && f.Info.StateList[0] == StateIntact)
                .ToList();
        }

        /// <summary>
        /// 按 ID 查找电闸。返回 (设备, 错误信息)；不存在时设备为 null。
        /// </summary>
        public static DeviceBase FindFusebox(int id, out string error)
        {
            error = null;
            if (Managers.Device.Cache.TryGetValue(id, out var dev))
            {
                if (dev != null && dev.DeviceType == EDeviceType.Fusebox)
                    return dev;
                error = $"设备 #{id} 不是电闸（{dev?.DeviceType ?? EDeviceType.NoneDevice}）。";
                return null;
            }
            error = $"找不到设备 #{id}。";
            return null;
        }

        /// <summary>
        /// 获取电闸状态中文标签。
        /// </summary>
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

        /// <summary>
        /// 格式化电闸一行：#ID  房间  状态。
        /// </summary>
        public static string FormatLine(DeviceBase fusebox)
        {
            var (localized, _) = WeaponPacketHelper.GetRoomLabel(fusebox);
            return $"  #{fusebox.ID}  房间: {localized}  状态: {GetStateLabel(fusebox)}";
        }

        /// <summary>
        /// 发送 C_INTERACT_FUSEBOX 到指定电闸 ID。
        /// </summary>
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
        public static System.Collections.IEnumerator SendPackets(List<int> ids, string action, WebConsole console)
        {
            int sent = 0;
            foreach (int id in ids)
            {
                if (Managers.Network == null || Managers.Network.GameServer == null)
                {
                    console.Log($"【{action}】网络链路已断开，中止剩余发包。", LogLevel.Warning);
                    yield break;
                }
                if (Managers.Game == null || Managers.Game.State != EGameState.Survive)
                {
                    console.Log($"【{action}】已离开生存阶段，中止剩余发包。", LogLevel.Warning);
                    yield break;
                }

                SendInteract(id);
                sent++;
                console.Log($"【{action}】已发送 {sent}/{ids.Count}：电闸 #{id}", LogLevel.Message);

                if (sent < ids.Count)
                    yield return new WaitForSeconds(PacketInterval);
            }
            console.Log($"【{action}】全部完成：已发送 {sent}/{ids.Count} 个 C_INTERACT_FUSEBOX。", LogLevel.Message);
        }
    }
}
