using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.AttackRangeServer
{
    [HarmonyPatch(typeof(Util), nameof(Util.UnifiedAttackRect), MethodType.Getter)]
    internal static class AttackRangeServerRectPatch
    {
        private static void Postfix(ref RectInfo __result)
        {
            if (!Engine.Enabled<AttackRangeServerFeature>())
                return;
            float extra = AttackRangeServerFeature.ExtraForwardReach;
            if (extra <= 0f)
                return;

            __result.Pos.X -= extra;
            __result.Size.X += extra;
        }
    }
}
