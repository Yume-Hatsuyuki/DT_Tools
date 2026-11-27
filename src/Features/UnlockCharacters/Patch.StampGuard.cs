// 作用：为角色全解锁挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockCharacters
{
    [HarmonyPatch(typeof(SteamInventorySource), "StampNewlyAcquired")]
    internal static class UnlockCharactersStampGuardPatch
    {
        private static bool Prefix() => !Engine.Enabled<UnlockCharactersFeature>();
    }
}
