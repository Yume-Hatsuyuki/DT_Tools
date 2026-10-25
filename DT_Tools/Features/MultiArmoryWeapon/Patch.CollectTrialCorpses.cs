using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.MultiArmoryWeapon
{
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
