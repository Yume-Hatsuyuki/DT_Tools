// 作用：为表情包全解锁挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockEmotes
{
    [HarmonyPatch(typeof(SteamInventorySource), "StampNewlyAcquired")]
    internal static class UnlockEmotesStampGuardPatch
    {
        private static bool Prefix() => !Engine.Enabled<UnlockEmotesFeature>();
    }
}
