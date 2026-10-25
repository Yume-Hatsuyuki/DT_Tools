using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.MultiArmoryWeapon
{
    [HarmonyPatch(typeof(Server.Game.Armory), "SendWeapon")]
    internal static class MultiArmoryWeaponSendWeaponPatch
    {
        private static void Prefix(Server.Game.Armory __instance)
        {
            if (!Engine.Enabled<MultiArmoryWeaponFeature>())
                return;

            var dm = Server.Game.DeviceManager.Instance;
            if (dm?.CurrentArmory == null || dm.CurrentArmory == __instance)
                return;
            if (__instance.State != (int)EArmoryState.OpenArmory)
                return;

            int fromId = dm.CurrentArmory.ID;
            Traverse.Create(dm).Property("CurrentArmory").SetValue(__instance);
            Traverse.Create(dm).Field("_lastArmory").SetValue(__instance);
            Traverse.Create(dm).Property("ArmoryPos").SetValue(__instance.DeviceInfo.Pos);
            Log.Info<MultiArmoryWeaponFeature>($"拾取接管：CurrentArmory #{fromId} → #{__instance.ID}");
        }
    }
}
