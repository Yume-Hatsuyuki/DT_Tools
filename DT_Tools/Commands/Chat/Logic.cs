using System;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.Chat
{
    internal static class ChatLogic
    {
        public static bool TryResolveDevice(ChatArgs args, int myPlayerId,
            out DeviceBase device, out string error)
        {
            device = null;
            error = null;

            var all = Devices.AllOf(EDeviceType.ChatDevice);
            if (all.Count == 0)
            {
                error = "地图尚未加载或本地没有打字机数据（请进入对局生存阶段后再试）。";
                return false;
            }

            if (args.DeviceId.HasValue)
            {
                int id = args.DeviceId.Value;
                device = all.FirstOrDefault(d => d.ID == id);
                if (device == null)
                {
                    error = $"找不到打字机 #{id}。可用无参 /chat 查看全部 ID。";
                    return false;
                }

                int state = device.DeviceState;
                if (state != 0 && state != myPlayerId)
                {
                    error = $"打字机 #{id} 正被玩家 #{state} 占用，无法抢占。请换一台或等其离开。";
                    return false;
                }
                return true;
            }

            var free = all.Where(d => d.DeviceState == 0).ToList();
            if (free.Count > 0)
            {
                device = free[UnityEngine.Random.Range(0, free.Count)];
                return true;
            }

            var mine = all.Where(d => d.DeviceState == myPlayerId).ToList();
            if (mine.Count > 0)
            {
                device = mine[UnityEngine.Random.Range(0, mine.Count)];
                return true;
            }

            error = "当前全部打字机均被他人占用，请稍后再试或指定空闲 #id。";
            return false;
        }

        public static bool TrySend(int deviceId, string text, out string error)
        {
            error = null;
            var net = Managers.Network?.GameServer;
            if (net == null)
            {
                error = "未连接到 Host（GameServer 链路为空），无法发送 C_ 包。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "消息内容为空。";
                return false;
            }

            try
            {
                net.Send(new C_INTERACT_CHATDEVICE
                {
                    DeviceId = deviceId,
                    IsSecret = false,
                });

                net.Send(new C_CHAT_MESSAGE
                {
                    Type = EChatType.DeviceChat,
                    Text = text,
                    DeviceId = deviceId,
                });

                net.Send(new C_HANDLE_CHATDEVICE
                {
                    DeviceId = deviceId,
                });
            }
            catch (Exception ex)
            {
                error = $"发包异常: {ex.Message}";
                return false;
            }

            return true;
        }
    }
}
