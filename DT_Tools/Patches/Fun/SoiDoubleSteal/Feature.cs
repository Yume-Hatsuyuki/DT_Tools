using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.Fun.SoiDoubleSteal
{
    /// <summary>
    /// SOI（RuleBreaker 规则破坏者）偷技能加强：原版偷一次后技能被替换、无法再偷；
    /// 本功能允许整局偷 MaxSteals 次（默认 2 次），且偷到的技能**使用后**自动恢复
    /// RuleBreaker（技能变回来才能偷下一个）——天然满足"必须先用掉第一个才能偷第二个"。
    /// 实现：Hook 0.1.16b Server.Game/SkillComponent.cs UseRuleBreaker（偷技能入口）计数；
    /// Hook UseSkill（技能使用入口）——当正在用偷来的技能（Data.Type != RuleBreaker）
    /// 且未偷满时，Postfix 用 SOI 原角色数据 AllocateSkill 恢复 RuleBreaker。
    /// 被动技能（偷到 Raasrush/DetailCheck/MindControl 等）触发即视为"已使用"并恢复。
    /// 全部在房主进程执行，无需 IsHost 判定。
    /// </summary>
    [PatchFeature(
        "SOI 双偷：SOI 整局可偷 2 次技能，偷来的技能用掉后自动恢复原技能，才能偷下一个（服务端，房主装生效）。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "花语")]
    public sealed class SoiDoubleStealFeature
    {
        [Config("SOI 整局可偷技能的总次数。", Min = 1, Max = 5)]
        public static int MaxSteals = 2;
    }
}
