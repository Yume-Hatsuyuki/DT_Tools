using System;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.Chat
{
    /// <summary>
    /// /chat 业务：打字机选取 + 占机/发言/释放发包序列。
    /// 包路径对齐 0.1.16a：
    ///   C_INTERACT_CHATDEVICE → DeviceManager.Interact → ChatDevice.Enter
    ///   C_CHAT_MESSAGE(DeviceChat) → RelayDeviceChat（要求已占机）
    ///   C_HANDLE_CHATDEVICE → DeviceManager.HandleEvent → ChatDevice.Exit
    /// </summary>
    internal static class ChatLogic
    {
        /// <summary>
        /// 解析目标机台。指定 ID 时校验存在且为本机 ChatDevice；
        /// 随机时优先空闲（DeviceState==0），否则本机已占的，再否则任意一台。
        /// </summary>
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

                // 被他人占用时服务端 Interact 静默失败（ChatDevice.Interact 仅 _user==null 或自己可进）
                int state = device.DeviceState;
                if (state != 0 && state != myPlayerId)
                {
                    error = $"打字机 #{id} 正被玩家 #{state} 占用，无法抢占。请换一台或等其离开。";
                    return false;
                }
                return true;
            }

            // 随机：空闲 → 本机已占 → 任意
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

            // 全部被他人占用——仍挑一台尝试（服务端会拒），给明确提示
            error = "当前全部打字机均被他人占用，请稍后再试或指定空闲 #id。";
            return false;
        }

        /// <summary>
        /// 发送序列：占机 → DeviceChat 消息 → 释放。
        /// 三包同帧发出，服务端 room.Push FIFO，Interact 先于 Chat 执行，占机状态可被 RelayDeviceChat 看到。
        /// </summary>
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
                // 1. 占机（IsSecret=false → 普通打字机，非密聊）
                net.Send(new C_INTERACT_CHATDEVICE
                {
                    DeviceId = deviceId,
                    IsSecret = false,
                });

                // 2. 发言（与 VoiceManager.SendDeviceChatMessage 同构）
                net.Send(new C_CHAT_MESSAGE
                {
                    Type = EChatType.DeviceChat,
                    Text = text,
                    DeviceId = deviceId,
                });

                // 3. 释放占机，避免长时间霸占
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
