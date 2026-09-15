using System;
using System.Text;
using BepInEx.Logging;
using Protocol;

namespace DT_Tools.Console.Commands.Weapon
{
    /// <summary>
    /// /hand_weapon &lt;#playerId&gt;
    ///
    /// 营图：黑幕无视距离把手中凶器交给任意一名 White（客户端发包，跟随控制台，非房主可用）。
    ///
    /// 为什么不支持 all：
    ///   服务端 Player.HandWeapon 成功时先 RemoveWeapon() 删掉自己唯一的刀，再给目标创建 2001；
    ///   第一包生效后自己 Weapon==null，后续包全部被拒，且目标必须是存活、空手的 White。
    ///   一把刀只能递交一次，因此仅支持单体 #id（与 /call_c 的身份包同理）。
    ///
    /// 权限 / 前置条件（对齐服务端 Player.HandWeapon 的校验，提前在本机拦截）：
    ///   - 生存阶段；
    ///   - 本机颜色必须是 Dark（黑幕），且 Inventory.Weapon 有刀；
    ///   - 目标存在、不是自己；已知 Black（KnownBlackIds，黑幕通过 S_NOTIFY_BLACK 掌握）
    ///     的目标会被拒绝，因为服务端只接受 White；
    ///   - 其余条件（目标存活、空手）客户端无权威视图，交由服务端静默裁决。
    ///
    /// 实现原理：
    ///   原版 MyPlayer.UseHandWeapon 要求 224 单位内且无墙体遮挡的目标
    ///   （GetHandWeaponTarget），但服务端 HandWeapon 全程没有距离/LOS/阶段检查
    ///   （该包甚至不经过 DeviceManager，无 InteractLock），直接发 C_HAND_WEAPON 即可。
    ///
    /// 示例:
    ///   /营图 #3
    ///   /hand_weapon 5
    /// </summary>
    internal sealed class HandWeaponCommand : IConsoleCommand
    {
        public string   Name        => "hand_weapon";
        public string[] Aliases     => new[] { "营图" };
        public string   Usage       => "hand_weapon <#playerId>";
        public string   Description => "营图：黑幕无视距离把凶器交给指定 White（仅单体目标，一把刀只能递一次）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            var my = WeaponPacketHelper.RequireLocalPlayer(console);
            if (my == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return;
            }
            if (!WeaponPacketHelper.RequireSurvive(console))
            {
                console.SetResult("{\"ok\":false,\"error\":\"invalid state\"}");
                return;
            }

            if (my.Color != EPlayerColor.Dark)
            {
                console.Log($"本机颜色为 {my.Color}，递刀服务端要求 Dark（黑幕）身份。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"not dark\"}");
                return;
            }
            if (my.Inventory.Weapon.DataId == 0)
            {
                console.Log("手上没有凶器（Inventory.Weapon 为空），可先用 /天匠 截刀。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no weapon\"}");
                return;
            }

            if (args.Length == 0)
            {
                console.Log("用法: /hand_weapon <#playerId>\n不支持 all：一把刀只能递交一次（服务端成功后立即移除黑幕的刀）。",
                    LogLevel.Info);
                console.SetResult("{\"ok\":false,\"error\":\"usage\"}");
                return;
            }

            string token = args[0];
            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                console.Log("营图不支持 all：服务端 HandWeapon 一次只转移唯一一把刀，成功后黑幕手上立即清空，后续包必被拒。",
                    LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"all not supported\"}");
                return;
            }
            if (!WeaponPacketHelper.TryParseId(token, out int targetId, out string idErr))
            {
                console.Log(idErr, LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"invalid target\"}");
                return;
            }
            if (targetId == my.PublicInfo.PlayerId)
            {
                console.Log("目标不能是自己。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"target is self\"}");
                return;
            }

            var target = WeaponPacketHelper.FindClientPlayer(targetId);
            if (target == null)
            {
                console.Log($"本机玩家表中找不到 #{targetId}（未同屏/未进入对局？）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"target not found\"}");
                return;
            }
            if (Managers.Player.KnownBlackIds != null && Managers.Player.KnownBlackIds.Contains(targetId))
            {
                console.Log($"目标 {target.Name}（#{targetId}）已是已知 Black，服务端只接受空手 White 作为递刀对象。",
                    LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"target is black\"}");
                return;
            }

            Managers.Network.GameServer.Send(new C_HAND_WEAPON
            {
                TargetId = targetId
            });

            var msg = new StringBuilder();
            msg.Append($"【营图】已发送 C_HAND_WEAPON：将凶器递给 {target.Name}（#{targetId}），无视距离。");
            msg.Append("\n服务端无成功回执：自己手上的刀被移除（S_REMOVE_ITEM）即生效；");
            msg.Append("若目标已死亡/已有刀/身份非 White，服务端会静默拒绝。");
            console.Log(msg.ToString(), LogLevel.Message);

            console.SetResult("{\"ok\":true,\"targetId\":" + targetId + "}");
        }
    }
}
