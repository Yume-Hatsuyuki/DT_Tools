using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CharacterMapPin
{
    [HarmonyPatch(typeof(TabletManager), nameof(TabletManager.DeletePin))]
    internal static class CharacterMapPinKeepDeadPinPatch
    {
        private static bool Prefix(int id)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return true;

            if (!CharacterMapPinFeature.KeepDeadPin)
                return true;

            if (!CharacterMapPinLogic.CanKeepPin(id))
                return true;

            CharacterMapPinState.KeptPinIds.Add(id);
            return false;
        }
    }
}
