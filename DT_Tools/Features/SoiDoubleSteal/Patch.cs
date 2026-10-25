using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SoiDoubleSteal
{
    internal static class SoiDoubleStealLogic
    {
        private static readonly Dictionary<int, int> _stealCount = new Dictionary<int, int>();

        public static void OnSteal(Server.Game.Player owner)
        {
            if (owner == null) return;
            int id = owner.PublicInfo.PlayerId;
            _stealCount.TryGetValue(id, out int count);
            _stealCount[id] = count + 1;
        }

        public static bool IsUsingStolenSkill(Server.Game.Player owner)
        {
            if (owner?.SkillComponent?.Data == null) return false;
            int id = owner.PublicInfo.PlayerId;
            return _stealCount.TryGetValue(id, out int count) && count > 0
                && owner.SkillComponent.Data.Type != ESkillType.RuleBreaker;
        }

        public static void RestoreRuleBreaker(Server.Game.Player owner)
        {
            if (owner == null || owner.SkillComponent == null) return;
            int id = owner.PublicInfo.PlayerId;
            if (!_stealCount.TryGetValue(id, out int count) || count <= 0) return;
            if (count >= SoiDoubleStealFeature.MaxSteals)
                return;

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
                Log.Warn<SoiDoubleStealFeature>(ex, "SOI 偷技能计数失败（可忽略）");
            }
        }
    }

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
                Log.Warn<SoiDoubleStealFeature>(ex, "SOI 技能恢复失败（可忽略）");
            }
        }
    }
}
