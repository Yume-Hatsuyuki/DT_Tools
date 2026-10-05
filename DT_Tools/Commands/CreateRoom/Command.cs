using System;
using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.Game;

namespace DT_Tools.Commands.CreateRoom
{
    /// <summary>
    /// /create_room [名称] [public|private] — 从大厅创建联机房间（本机成为房主）。
    /// 复刻原版创建入口编排（0.1.16b UI_LobbyScene.cs:1198 OnClickCreateButton）：
    /// 语音预连 → School 资源域 → Steam 建房。建房是异步流程：本命令立即返回，
    /// 结果进日志，房间码用 /room_code 查看。房名合法性原版只在 UI 输入框校验
    /// （OnValidateRoomName），Steam 元数据不限制，此处仅做非空+长度兜底。
    /// </summary>
    internal sealed class CreateRoomCommand : ICommand
    {
        public string Name => "create_room";
        public string[] Aliases => new[] { "建房" };
        public string Usage => "create_room [名称] [public|private]";
        public string Description => "从大厅创建联机房间并成为房主（异步，结果进日志；名称默认「DT 房间」，公开/私密不填跟随大厅 UI 选择）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (RoomFlow.IsInRoom)
            {
                ctx.Reply("已在房间中。先 /exit_room 再创建新房间。");
                return CommandResult.Fail("already in room");
            }

            string privacy = null;
            var nameParts = new List<string>();
            foreach (string arg in ctx.Args)
            {
                if (IsPrivacyToken(arg, out string canonical))
                    privacy = canonical;
                else
                    nameParts.Add(arg);
            }
            string name = nameParts.Count > 0 ? string.Join(" ", nameParts) : "DT 房间";
            if (string.IsNullOrWhiteSpace(name) || name.Length > 30)
            {
                ctx.Reply("房间名需为 1–30 个字符。用法: " + Usage);
                return CommandResult.Fail("invalid name");
            }

            ctx.Reply($"正在创建房间「{name}」（{(privacy ?? "跟随大厅选择")}）…结果进日志，完成后用 /room_code 查看。");
            CreateRoomLogic.StartCreateFlow(name, privacy);
            return CommandResult.Success(new { name, privacy });
        }

        private static bool IsPrivacyToken(string arg, out string canonical)
        {
            if (arg.Equals("public", StringComparison.OrdinalIgnoreCase) || arg == "公开")
            {
                canonical = "public";
                return true;
            }
            if (arg.Equals("private", StringComparison.OrdinalIgnoreCase) || arg == "私密")
            {
                canonical = "private";
                return true;
            }
            canonical = null;
            return false;
        }
    }
}
