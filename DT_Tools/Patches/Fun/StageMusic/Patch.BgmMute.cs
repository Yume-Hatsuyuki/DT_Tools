using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.StageMusic
{
    /// <summary>
    /// 原版 BGM 静音：SoundManager.PlayBGM（0.1.15b SoundManager.cs:137）/PlayBGMWithIntro
    /// （:175，签名多变故字符串定位）直接跳过——各阶段原版会播 DetectiveBGM/TrialMainBGM/
    /// Beta_Result_BGM/MainTitleBGM 等，与自定义阶段音乐混音；MuteVanillaBgm 开启时启用。
    /// </summary>
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
