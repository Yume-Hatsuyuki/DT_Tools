using DT_Tools.Commands;

namespace DT_Tools.Commands.HandWeapon
{
    /// <summary>
    /// /hand_weapon &lt;#playerId&gt; — 营图：黑幕无视距离把手中凶器交给任意一名 White
    /// （客户端发包，跟随控制台，非房主可用）。
    ///
    /// 为什么不支持 all：服务端 Player.HandWeapon（0.1.15b Server.Game/Player.cs:1600）
    /// 成功时先移除自己唯一的刀，再给目标创建凶器；第一包生效后自己 Weapon==null，
    /// 后续包全部被拒，且目标必须是存活、空手的 White。一把刀只能递交一次，
    /// 因此仅支持单体 #id（与 /call_c 的身份包同理）。
    ///
    /// 权限 / 前置条件（对齐服务端 Player.HandWeapon 的校验，提前在本机拦截）：
    ///   - 生存阶段；
    ///   - 本机颜色必须是 Dark（黑幕），且 Inventory.Weapon 有刀；
    ///   - 目标存在、不是自己；已知 Black（KnownBlackIds，黑幕通过 S_NOTIFY_BLACK 掌握，
    ///     0.1.15b PlayerManager.cs:50）的目标会被拒绝，因为服务端只接受 White；
    ///   - 其余条件（目标存活、空手）客户端无权威视图，交由服务端静默裁决。
    ///
    /// 实现原理：原版 MyPlayer.UseHandWeapon 要求 224 单位内且无墙体遮挡的目标
    /// （GetHandWeaponTarget），但服务端 HandWeapon 全程没有距离/LOS/阶段检查
    /// （该包甚至不经过 DeviceManager，无 InteractLock），直接发 C_HAND_WEAPON 即可。
    ///
    /// 示例:
    ///   /营图 #3
    ///   /hand_weapon 5
    /// </summary>
    internal sealed class HandWeaponCommand : ICommand
    {
        public string Name => "hand_weapon";
        public string[] Aliases => new[] { "营图" };
        public string Usage => "hand_weapon <#playerId>";
        public string Description => "营图：黑幕无视距离把凶器交给指定 White（仅单体目标，一把刀只能递一次）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx) => HandWeaponLogic.Execute(ctx);
    }
}
