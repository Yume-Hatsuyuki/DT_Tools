// 作用：为表情包全解锁挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.UnlockEmotes
{
    [HarmonyPatch(typeof(SteamInventorySource), nameof(SteamInventorySource.IsEmoticonOwned))]
    internal static class UnlockEmotesIsEmoticonOwnedPatch
    {
        private static bool Prefix(int id, ref bool __result)
        {
            if (!Engine.Enabled<UnlockEmotesFeature>())
                return true;

            if (id <= 0)
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }
    }
}
