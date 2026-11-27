// 作用：实现 /chat 命令的参数解析与执行；不负责命令注册与其他命令。
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.Chat
{
    internal sealed class ChatCommand : ICommand
    {
        public string Name => "chat";
        public string[] Aliases => new[] { "打字机", "打字", "device_chat" };
        public string Usage => "chat [#deviceId] <text>";
        public string Description => "通过打字机发送全图文字：无参数列出 ID/位置；#id 指定机台；不写 id 则随机空闲机台。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.UiChat | FeatureTag.GameDevice;
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                var devices = Devices.AllOf(EDeviceType.ChatDevice);
                ctx.Reply(ChatFormat.HelpAndList(devices));
                return CommandResult.Success(ChatFormat.ListResult(devices));
            }

            if (!LocalPlayer.TryGetPlayer(out var my, out string localError))
            {
                ctx.Reply(localError);
                return CommandResult.Fail("not in game");
            }

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

            if (!ChatArgs.TryParse(ctx.Args, out var args, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("bad args");
            }

            if (!ChatLogic.TryResolveDevice(args, my.PublicInfo.PlayerId, out var device, out string resolveError))
            {
                ctx.Reply(resolveError);
                return CommandResult.Fail("no device");
            }

            int deviceId = device.ID;
            string text = args.Text;
            var (roomLoc, roomRaw) = RoomLabel.FromDevice(device);
            var deferred = ctx.Defer();
            ChatLogic.StartSend(deviceId, my.PublicInfo.PlayerId, text, (ok, error, remaining) =>
            {
                if (ok)
                {
                    ctx.Reply($"已通过打字机 #{deviceId}（{roomLoc}）发送：{text}（聊天预算剩余 {remaining}/5）");
                    deferred?.Complete(CommandResult.Success(ChatFormat.SendResult(device, roomRaw, text)));
                }
                else
                {
                    ctx.Warn($"打字机发送未完成：{error}");
                    deferred?.Complete(CommandResult.Fail(error));
                }
            });

            if (deferred == null)
            {
                ctx.Reply("已发起打字机发送，占用与预算确认结果见后续日志。");
                return CommandResult.Success(new { scheduled = true, deviceId });
            }
            return deferred;
        }
    }
}
