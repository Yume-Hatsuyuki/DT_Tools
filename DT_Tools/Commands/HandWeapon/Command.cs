using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.HandWeapon
{
    /// <summary>
    /// /hand_weapon &lt;#playerId>：营图，黑幕无视距离把手中凶器交给一名 White（客户端发包，跟随控制台，非房主可用）。
    /// </summary>
    /// <remarks>
    /// 只支持单体 #id：服务端 Player.HandWeapon（0.1.18c Server.Game/Player.cs:1600）成功后即移除自己唯一的刀，之后的包全部被拒，一把刀只能递交一次。
    /// 本机预检（与服务端校验一致，提前拦截）：生存阶段；本机为 Dark 且持有刀；目标存在、不是自己，且不在 KnownBlackIds 中（服务端只接受 White；0.1.16b PlayerManager.cs:54（待核对））。
    /// 目标存活与空手由服务端裁决。不经 DeviceManager，无 InteractLock，直接发 C_HAND_WEAPON。
    /// 原版 MyPlayer.UseHandWeapon 要求 224 单位内且无遮挡，但服务端 HandWeapon 没有距离、视线与阶段检查，本命令因此不做距离判断。
    /// 示例：/营图 #3
    /// </remarks>
    internal sealed class HandWeaponCommand : ICommand
    {
        public string Name => "hand_weapon";
        public string[] Aliases => new[] { "营图" };
        public string Usage => "hand_weapon <#playerId>";
        public string Description => "营图：黑幕无视距离把凶器交给指定 White（仅单体目标，一把刀只能递一次）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameWeapon;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx) => HandWeaponLogic.Execute(ctx);
    }
}
