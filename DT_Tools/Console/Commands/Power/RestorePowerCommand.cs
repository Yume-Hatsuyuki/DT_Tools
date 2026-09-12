namespace DT_Tools.Console.Commands.Power
{
    /// <summary>
    /// /restore_power
    ///
    /// 强制恢复全场电力（仅房主、仅生存阶段）。
    ///
    /// 直接复用原版 DeviceManager.RefuseAllFuse：重连全部已断开的电箱
    /// （Fusebox.ConnetCable）。断开计数归 0 时 AreaManager.RefreshLight
    /// 点亮全部区域并广播 FuseOnSfx，同时安排 60 秒后由
    /// DeviceManager.StartFuseboxSabotage 重新随机发放 3 个电箱破坏任务。
    /// 等效于所有损坏电箱被玩家修完，不影响玩家正在进行的正常修线流程。
    ///
    /// 示例:
    ///   /restore_power
    /// </summary>
    internal sealed class RestorePowerCommand : IConsoleCommand
    {
        public string   Name        => "restore_power";
        public string[] Aliases     => new[] { "power_on", "开灯", "来电", "复电", "电力恢复", "恢复电力" };
        public string   Usage       => "restore_power";
        public string   Description => "强制恢复全场电力（等效所有损坏电箱被修完，需房主·生存阶段）。";
        public string   Author      => "梦初雪";

        public void Execute(string[] args, WebConsole console)
        {
            PowerControlHelper.RestorePower(console);
        }
    }
}
