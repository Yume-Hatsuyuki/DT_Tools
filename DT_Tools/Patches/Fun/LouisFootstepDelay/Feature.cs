using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.LouisFootstepDelay
{
    /// <summary>
    /// 路易斯警报脚印延长：路易斯技能（Telekinesis 警报标记，DeadDetective buff）标记的
    /// 玩家被杀死后，全图警报（警报音 + 尸体箭头，0.1.16b Player.OnDead 内）的同时，
    /// 凶手会获得脚印追踪（每 300ms 记录凶手脚印，全图重放 Footstep）。
    /// 原版脚印追踪 3000ms（3 秒），本功能在路易斯标记场景下延长为 FootprintMs（默认 5000ms/5 秒）。
    /// 实现：Hook 0.1.16b Server.Game/Player.cs:883 OnDeadMurder（持刀与处决技死亡的唯一入口，
    /// CollarBomb 走 OnDeadCollarBomb 不经此），Postfix 检测死者带 DeadDetective
    /// （该 buff 仅路易斯技能可施加，天然满足"只在路易斯角色时生效"）→ 给凶手提前
    /// AddBuff(Footprint, FootprintMs)，原逻辑随后 AddBuff(Footprint, 3000) 因已有该 buff
    /// 自动跳过（BuffComponent.AddBuff 有 Contains 判重），总时长即 FootprintMs。
    /// 未标记的普通杀人保持原版 3 秒，不受影响。
    /// </summary>
    [PatchFeature(
        "路易斯警报脚印延长：被路易斯警报标记的玩家死后，凶手脚印追踪从 3 秒延长为 5 秒（仅路易斯标记场景生效，普通杀人保持原版）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "花语")]
    public sealed class LouisFootstepDelayFeature
    {
        [Config("被路易斯标记的玩家死后，凶手脚印追踪时长（毫秒，原版 3000）。", Min = 1000, Max = 20000)]
        public static int FootprintMs = 5000;
    }
}
