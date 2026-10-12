using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.MultiArmoryWeapon
{
    /// CollectTrialCorpses 前缀（0.1.17a Server.Game/GameRoom.cs:1951-1967）：
    /// 原版只把「与本庭绑定凶手同 BlackId」的未发现尸体编入庭审；多刀下多名黑方各自杀人时，
    /// 其他凶手的尸体会被过滤掉 → 不会走 MarkDiscoveredForTrial，DiscoverdDone/DiscoverTime 一直为假/0。
    /// 随后 Corpse.GetDisplayTime（:315-345）在 DiscoverTime==0 时落到估算分支，扫尸体显示 00:00，
    /// 尽管 CorpseMeta.DeadTime 在构造时已正确写入 SurviveTime。
    /// 开启多刀后放宽为：所有未发现、非炸弹、非隐藏、非 TrickLocked 的尸体均可编入（不再按 BlackId 过滤）。
    [HarmonyPatch(typeof(Server.Game.GameRoom), nameof(Server.Game.GameRoom.CollectTrialCorpses))]
    internal static class MultiArmoryWeaponCollectTrialCorpsesPatch
    {
        private static bool Prefix(
            Server.Game.GameRoom __instance,
            Server.Game.Corpse primary,
            Server.Game.Player primaryBlack,
            ref List<Server.Game.Corpse> __result)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>())
                return true;

            List<Server.Game.Corpse> list = new List<Server.Game.Corpse>();
            if (primary != null)
                list.Add(primary);

            Server.Game.DeviceManager dm = Server.Game.DeviceManager.Instance;
            if (dm?.Corpses != null)
            {
                foreach (Server.Game.Corpse corpse in dm.Corpses)
                {
                    if (corpse == null || corpse == primary)
                        continue;
                    if (corpse.DiscoverdDone || corpse.IsBombCorpse || corpse.IsHidden || corpse.TrickLocked)
                        continue;
                    list.Add(corpse);
                }
            }

            list.Sort((a, b) => __instance.GetCorpseDeadTime(a.ID).CompareTo(__instance.GetCorpseDeadTime(b.ID)));
            if (list.Count > 1)
            {
                Log.Info<MultiArmoryWeaponFeature>(
                    $"多刀庭审编入 {list.Count} 具尸体（含其他黑方击杀）：[{string.Join(",", list.Select(c => c.ID))}]，本庭绑定凶手 #{primaryBlack?.PublicInfo.PlayerId ?? 0}");
            }

            __result = list;
            return false;
        }
    }
}
