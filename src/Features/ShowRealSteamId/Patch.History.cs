// 作用：在生成战绩记录时恢复被隐藏身份掩码的 SteamID 与 Steam 昵称；不改动既有记录与网络身份。
using System;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.ShowRealSteamId
{
    [HarmonyPatch(typeof(GameHistoryStorage), "BuildFromRoster",
        new[] { typeof(S_PLAYER_ROSTER), typeof(ulong), typeof(bool) })]
    internal static class RealSteamIdHistoryPatch
    {
        private static readonly MethodInfo GetSteamNameMethod =
            AccessTools.Method(typeof(GameHistoryStorage), "GetSteamName", new[] { typeof(ulong) });

        private static void Postfix(GameHistoryStorage.GameRecord __result)
        {
            if (!Engine.Enabled<ShowRealSteamIdFeature>() || __result == null)
                return;

            try
            {
                if (!string.IsNullOrEmpty(__result.HostSealedId))
                    __result.HostSealedId = null;

                foreach (GameHistoryStorage.Participant player in __result.Players)
                {
                    if (!player.IdentityHidden)
                        continue;

                    ulong steamId = player.SteamId;
                    player.SealedId = null;
                    if (steamId != 0L)
                        player.SteamName = ResolveSteamName(steamId);
                }
            }
            catch (Exception ex)
            {
                Log.Warn<ShowRealSteamIdFeature>(ex, "战绩记录还原真实 SteamID 失败");
            }
        }

        private static string ResolveSteamName(ulong steamId)
        {
            try
            {
                return GetSteamNameMethod?.Invoke(null, new object[] { steamId }) as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
