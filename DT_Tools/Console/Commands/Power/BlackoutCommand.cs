namespace DT_Tools.Console.Commands.Power
{
    /// <summary>
    /// /blackout
    ///
    /// 强制触发全场停电（仅房主、仅生存阶段）。
    ///
    /// 复用 Dark 剪断电箱线的原版完整路径：Fusebox.Interact → DisconnetCable，
    /// 断开第 2 个电箱时 AreaManager.RefreshLight 将所有区域置暗，
    /// 各 Area.IsLight setter 向区域内玩家单播 S_AREA_PUBLIC；
    /// 客户端 Darkness=true：关闭全局灯光与交互读条，仅保留玩家身边小聚光灯，
    /// 同时 Luna 的 Catastrophe 护盾在停电期间失效。
    ///
    /// 已被断开的电箱计入阈值；停电后电箱保持损坏状态，玩家可正常修线，
    /// 也可用 /restore_power 一键恢复。
    ///
    /// 示例:
    ///   /blackout
    /// </summary>
    internal sealed class BlackoutCommand : IConsoleCommand
    {
        public string   Name        => "blackout";
        public string[] Aliases     => new[] { "关灯", "停电", "断电", "熄灯" };
        public string   Usage       => "blackout";
        public string   Description => "强制触发全场停电（等效 Dark 断开第 2 个电箱，需房主·生存阶段）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            PowerControlHelper.Blackout(console);
        }
    }
}
