using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.StageMusic
{
    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlayBGM))]
    internal static class StageMusicBgmMutePatch
    {
        private static bool Prefix()
            => !(Engine.Enabled<StageMusicFeature>() && StageMusicFeature.MuteVanillaBgm);
    }

    [HarmonyPatch(typeof(SoundManager), "PlayBGMWithIntro")]
    internal static class StageMusicBgmIntroMutePatch
    {
        private static bool Prefix()
            => !(Engine.Enabled<StageMusicFeature>() && StageMusicFeature.MuteVanillaBgm);
    }
}
