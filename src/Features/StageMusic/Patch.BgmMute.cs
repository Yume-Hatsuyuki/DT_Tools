// 作用：为[实验性] 阶段音乐挂载补丁拦截；不负责配置声明与状态存储。
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
