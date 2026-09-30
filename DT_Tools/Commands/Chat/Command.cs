using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.Chat
{
    /// <summary>
    /// /chat [#deviceId] &lt;text&gt; — 本机通过打字机（ChatDevice）发送全图文字。
    ///
    /// 三种用法：
    ///   无参数              → 帮助 + 列出本机缓存中全部打字机 ID 与所在地区
    ///   /chat #id 文本      → 指定打字机 ID 发送
    ///   /chat 文本          → 随机选用一台空闲（优先）打字机发送
    ///
    /// 实现路径（对齐原版 UI_ChatDevicePopup.Send → VoiceManager.SendDeviceChatMessage）：
    ///   1. C_INTERACT_CHATDEVICE 占机（服务端 ChatDevice.Enter，State=PlayerId）
    ///   2. C_CHAT_MESSAGE Type=DeviceChat（服务端 RelayDeviceChat 要求 Survive + 存活 + 已占机）
    ///   3. C_HANDLE_CHATDEVICE 释放占机
    /// 服务端 DeviceManager.Interact 对 ChatDevice 无距离校验，可在地图任意位置发包。
    /// 仅生存阶段生效（0.1.15b HostPacketHandler.RelayDeviceChat）。
    /// 跟随控制台 / 客户端，非房主可用。
    /// </summary>
    internal sealed class ChatCommand : ICommand
    {
        public string Name => "chat";
        public string[] Aliases => new[] { "打字机", "打字", "device_chat" };
        public string Usage => "chat [#deviceId] <text>";
        public string Description => "通过打字机发送全图文字：无参数列出 ID/位置；#id 指定机台；不写 id 则随机空闲机台。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            // ── 无参数：帮助 + 打字机列表 ──
            if (ctx.Args.Length == 0)
            {
                var devices = Devices.AllOf(EDeviceType.ChatDevice);
                ctx.Reply(ChatFormat.HelpAndList(devices));
                return CommandResult.Success(ChatFormat.ListResult(devices));
            }

            // ── 本机身份 + 网络链路 ──
            if (!LocalPlayer.TryGetPlayer(out var my, out string localError))
            {
                ctx.Reply(localError);
                return CommandResult.Fail("not in game");
            }

            // ── 阶段 / 存活门禁（服务端 RelayDeviceChat 硬条件） ──
            if (!LocalPlayer.IsSurvive)
            {
                ctx.Reply($"当前阶段 {LocalPlayer.StateText} 无法使用打字机（仅 Survive 阶段生效）。");
                return CommandResult.Fail("invalid state");
            }
            if (Managers.Game != null && !Managers.Game.IsAlive)
            {
                ctx.Reply("本机已死亡，无法通过打字机发言（服务端要求 sender.IsAlive）。");
                return CommandResult.Fail("dead");
            }

            // ── 解析参数 ──
            if (!ChatArgs.TryParse(ctx.Args, out var args, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("bad args");
            }

            // ── 选取机台 ──
            if (!ChatLogic.TryResolveDevice(args, my.PublicInfo.PlayerId, out var device, out string resolveError))
            {
                ctx.Reply(resolveError);
                return CommandResult.Fail("no device");
            }

            // ── 发包：占机 → 发言 → 释放 ──
            if (!ChatLogic.TrySend(device.ID, args.Text, out string sendError))
            {
                ctx.Reply(sendError);
                return CommandResult.Fail("send failed");
            }

            var (roomLoc, roomRaw) = RoomLabel.FromDevice(device);
            ctx.Reply($"已通过打字机 #{device.ID}（{roomLoc}）发送：{args.Text}");
            return CommandResult.Success(ChatFormat.SendResult(device, roomRaw, args.Text));
        }
    }
}
