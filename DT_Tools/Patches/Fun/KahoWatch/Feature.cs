using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.KahoWatch
{
    /// <summary>
    /// KAHO 监视提示：KAHO 用技能（服从规则，UseComplyRules）监视某人后，被监视者
    /// 拔刀（C_HAND_WEAPON）、拆电闸（C_HANDLE_FUSEBOX）、刀人成功（OnDeadMurder）时，
    /// KAHO 会收到聊天提示（服务端私发 S_CHAT_MESSAGE，仅 KAHO 可见）。
    /// Hook 全部在房主进程（HostPacketHandler/Player/DeviceManager 服务端链路），
    /// 无需 IsHost 判定；提示带冷却防刷屏。
    /// </summary>
    [PatchFeature(
        "KAHO 监视提示：KAHO 监视某人后，被监视者拔刀/拆电闸/刀人成功时 KAHO 收到聊天提示（服务端，房主装生效，全房无需装）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "花语")]
    public sealed class KahoWatchFeature
    {
        [Config("同一种动作的提示冷却（毫秒），防止连续触发刷屏。", Min = 1000, Max = 60000)]
        public static int CooldownMs = 5000;

        [Config("被监视者拔刀时提示 KAHO。")]
        public static bool NotifyDrawWeapon = true;

        [Config("被监视者拆电闸时提示 KAHO。")]
        public static bool NotifyBreakFusebox = true;

        [Config("被监视者刀人成功（目标死亡）时提示 KAHO。")]
        public static bool NotifyKill = true;
    }
}
