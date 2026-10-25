using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.ShowTrueName
{
    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.Dead))]
    internal static class ShowTrueNameDeadPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<ShowTrueNameFeature>())
                return;

            if (!ShowTrueNameFeature.ShowWhileDead)
                return;
            Player my = Managers.Player.MyPlayer;
            if (my?.NameTag != null)
                my.NameTag.gameObject.SetActive(true);
        }
    }
}
