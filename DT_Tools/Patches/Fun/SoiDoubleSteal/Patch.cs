using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.SoiDoubleSteal
{
    /// <summary>SOI 偷技能计数与恢复（服务端进程内共享状态）。</summary>
    internal static class SoiDoubleStealLogic
    {
        /// <summary>各 SOI 玩家（PlayerId）本局已偷次数。</summary>
        private static readonly Dictionary<int, int> _stealCount = new Dictionary<int, int>();

        /// <summary>UseRuleBreaker 前缀：记录本次偷技能次数。</summary>
        public static void OnSteal(Server.Game.Player owner)
        {
            if (owner == null) return;
            int id = owner.PublicInfo.PlayerId;
            _stealCount.TryGetValue(id, out int count);
            _stealCount[id] = count + 1;
        }

        /// <summary>判断该玩家是否正在使用偷来的技能（SOI 且已偷过、当前技能不是 RuleBreaker）。</summary>
        public static bool IsUsingStolenSkill(Server.Game.Player owner)
        {
            if (owner?.SkillComponent?.Data == null) return false;
            int id = owner.PublicInfo.PlayerId;
            return _stealCount.TryGetValue(id, out int count) && count > 0
                && owner.SkillComponent.Data.Type != ESkillType.RuleBreaker;
        }

        /// <summary>偷来的技能使用后恢复 RuleBreaker（未偷满才恢复；偷满后不再恢复，整局即止）。</summary>
        public static void RestoreRuleBreaker(Server.Game.Player owner)
        {
            if (owner == null || owner.SkillComponent == null) return;
            int id = owner.PublicInfo.PlayerId;
            if (!_stealCount.TryGetValue(id, out int count) || count <= 0) return;
            if (count >= SoiDoubleStealFeature.MaxSteals)
                return; // 已偷满：保持偷来的技能，之后无法再偷

            // 用 SOI 原角色数据重新分配技能 → Data 恢复为 RuleBreaker，并同步
            // SendChangeSkill / SkillState / CanUseSkill / 清理偷来技能的被动 buff
            var soi = Managers.Data.CharacterDic.Values
                .FirstOrDefault(x => x.Type == ECharacterType.Soi);
            if (soi == null)
            {
                Log.Warn<SoiDoubleStealFeature>("未找到 SOI 角色数据，技能恢复失败");
                return;
            }
            owner.SkillComponent.AllocateSkill(soi);
            Log.Info<SoiDoubleStealFeature>($"SOI 技能已恢复 RuleBreaker（已偷 {count} 次，可再偷）：{owner.Name}");
        }
    }

    /// <summary>偷技能计数（0.1.16b SkillComponent.UseRuleBreaker(int)，private）。</summary>
    [HarmonyPatch(typeof(Server.Game.SkillComponent), "UseRuleBreaker")]
    internal static class SoiDoubleStealCountPatch
    {
        private static void Prefix(Server.Game.SkillComponent __instance)
        {
            try
            {
                if (!Engine.Enabled<SoiDoubleStealFeature>())
                    return;
                SoiDoubleStealLogic.OnSteal(__instance?.Owner);
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<SoiDoubleStealFeature>("SOI 偷技能计数失败（可忽略）：" + ex.Message);
            }
        }
    }

    /// <summary>
    /// 技能使用入口（0.1.16b SkillComponent.UseSkill(C_USE_SKILL)，public）：正在使用
    /// 偷来的技能（Data.Type != RuleBreaker）时，用完后恢复 RuleBreaker，使其可偷下一个。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.SkillComponent), "UseSkill")]
    internal static class SoiDoubleStealUsePatch
    {
        private static void Postfix(Server.Game.SkillComponent __instance)
        {
            try
            {
                if (!Engine.Enabled<SoiDoubleStealFeature>())
                    return;
                var owner = __instance?.Owner;
                if (owner == null)
                    return;
                if (SoiDoubleStealLogic.IsUsingStolenSkill(owner))
                    SoiDoubleStealLogic.RestoreRuleBreaker(owner);
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<SoiDoubleStealFeature>("SOI 技能恢复失败（可忽略）：" + ex.Message);
            }
        }
    }
}
