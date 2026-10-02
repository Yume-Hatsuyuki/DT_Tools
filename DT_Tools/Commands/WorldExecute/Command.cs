using DT_Tools.Commands;

namespace DT_Tools.Commands.WorldExecute
{
    /// <summary>
    /// /world.execute me — 黑方自刀（Murder 路径），致敬 Mili《world.execute(me);》。
    ///
    /// 机制：服务端 <c>Player.UseWeapon</c>（0.1.16a Server.Game/Player.cs:1118-1166）
    /// 校验 CanAttack / Weapon / Survive / 目标存活 / 非 Hide / 距离，但<strong>不拒绝
    /// targetId == 自己</strong>。自瞄时距离为 0，通过后走 <c>OnDamaged → PushAfter(400)
    /// → OnDead(..., Murder)</c>（同文件 :799-811），与正常刀杀同路径出尸体。
    /// 客户端原版 <c>MyPlayer.UseWeaponItem</c>（0.1.16a MyPlayer.cs:704-750）要求
    /// AttackTargetPlayer，不会自瞄；本命令直接发 <c>C_KILL_PLAYER</c> 绕过 UI。
    ///
    /// 仅 Black + 持刀 + CanAttack + Survive + 存活 + 非 Hide 可用；白方无合法自死包。
    /// 无参数 / 参数非 me → 打印帮助（EXECUTE 字符画 + 伪 Java 会话）；正确用法：<c>/world.execute me</c>。
    /// 别名不含 execute：已被 /kill 占用（KillCommand.Aliases），注册表冲突会抛异常。
    /// 跟随控制台 / 客户端，非房主可用。
    /// </summary>
    internal sealed class WorldExecuteCommand : ICommand
    {
        public string Name => "world.execute";
        public string[] Aliases => new[] { "自杀", "自尽", "自刎归天", "execute" };
        public string Usage => "world.execute me";
        public string Description => "黑方自刀：/world.execute me 以 Murder 路径结束自己（仅持刀 Black · Survive）。";
        public string Author => "梦初雪";
        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx) => WorldExecuteLogic.Execute(ctx);
    }
}
