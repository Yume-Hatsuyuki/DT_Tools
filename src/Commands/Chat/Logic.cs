// 作用：实现 /Chat 命令的业务逻辑与原生打字机发送时序；不负责命令入口与注册。
using System;
using System.Collections;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.Chat
{
    internal static class ChatLogic
    {
        private const float BudgetMax = 5f;
        private const float BudgetPerSecond = 3f;
        private const float OccupancyTimeoutSeconds = 2f;
        private static readonly object BudgetLock = new object();
        private static float _tokens = BudgetMax;
        private static int _budgetTickMs = Environment.TickCount;

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

        public static bool StartSend(int deviceId, int myPlayerId, string text, Action<bool, string, int> done)
        {
            if (!TryTakeBudgetToken(out int remaining))
            {
                done?.Invoke(false, $"聊天预算不足（剩余 {remaining}/5，按每秒 3 条恢复），消息未发送。", remaining);
                return true;
            }

            CoroutineHost.Start(CoSend(deviceId, myPlayerId, text, remaining, done));
            return true;
        }

        private static IEnumerator CoSend(int deviceId, int myPlayerId, string text, int remaining,
            Action<bool, string, int> done)
        {
            var net = Managers.Network?.GameServer;
            if (net == null)
            {
                RefundBudgetToken();
                done?.Invoke(false, "未连接到 Host（GameServer 链路为空），无法发送 C_ 包。", remaining);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                RefundBudgetToken();
                done?.Invoke(false, "消息内容为空。", remaining);
                yield break;
            }

            try
            {
                net.Send(new C_INTERACT_CHATDEVICE
                {
                    DeviceId = deviceId,
                    IsSecret = false,
                });
            }
            catch (Exception ex)
            {
                RefundBudgetToken();
                done?.Invoke(false, $"进入打字机失败: {ex.Message}", remaining);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + OccupancyTimeoutSeconds;
            DeviceBase device = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                device = Devices.AllOf(EDeviceType.ChatDevice).FirstOrDefault(d => d.ID == deviceId);
                if (device != null && device.DeviceState == myPlayerId)
                    break;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            if (device == null || device.DeviceState != myPlayerId)
            {
                SendHandle(net, deviceId);
                RefundBudgetToken();
                done?.Invoke(false,
                    $"打字机 #{deviceId} 占用未确认（本地状态 {(device != null ? device.DeviceState.ToString() : "未知")}），消息未发送。",
                    remaining);
                yield break;
            }

            try
            {
                net.Send(new C_CHAT_MESSAGE
                {
                    Type = EChatType.DeviceChat,
                    Text = text,
                    DeviceId = deviceId,
                });
            }
            catch (Exception ex)
            {
                SendHandle(net, deviceId);
                RefundBudgetToken();
                done?.Invoke(false, $"发送失败: {ex.Message}", remaining);
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.05f);
            SendHandle(net, deviceId);
            done?.Invoke(true, null, remaining);
        }

        private static void SendHandle(DummyClient.IPacketSink net, int deviceId)
        {
            try
            {
                net.Send(new C_HANDLE_CHATDEVICE { DeviceId = deviceId });
            }
            catch (Exception)
            {
            }
        }

        private static bool TryTakeBudgetToken(out int remaining)
        {
            lock (BudgetLock)
            {
                int now = Environment.TickCount;
                float elapsed = Mathf.Max(0f, (now - _budgetTickMs) / 1000f);
                _budgetTickMs = now;
                _tokens = Mathf.Min(BudgetMax, _tokens + elapsed * BudgetPerSecond);
                if (_tokens < 1f)
                {
                    remaining = Mathf.FloorToInt(_tokens);
                    return false;
                }
                _tokens -= 1f;
                remaining = Mathf.FloorToInt(_tokens);
                return true;
            }
        }

        private static void RefundBudgetToken()
        {
            lock (BudgetLock)
            {
                _tokens = Mathf.Min(BudgetMax, _tokens + 1f);
            }
        }
    }
}
