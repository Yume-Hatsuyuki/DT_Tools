using System;
using DummyClient;
using HarmonyLib;
using Steamworks;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LobbyDisplayCount
{
    [HarmonyPatch]
    [PatchFeature(
        "自定义大厅显示人数：对外（Steam 大厅列表）显示人数可与房间真实人数不同（偏移或固定值）。真实满员判定不变——达到最大人数依旧拒绝加入；与 SpectatorJoin（满房观战）、LobbyMaxPlayers（超上限）天然兼容。仅房主生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.SysRoom | FeatureTag.SysNetwork,
        Author = "合理")]
    public sealed class LobbyDisplayCountFeature
    {
        [Config("显示人数偏移：对外显示人数 = 房间真实人数 + 偏移（可为负）。结果钳制下限 1（Steam 大厅列表读不到 <=0 的值会回退显示真实 Steam 成员数）。", Min = -64)]
        public static int Offset = 0;

        [Config("固定显示人数：>= 0 时直接对外显示该值（忽略偏移）；-1 表示不启用固定值。同样钳制下限 1。", Min = -1)]
        public static int FixedCount = -1;

        private static int DisplayCount(int realCount)
        {
            int displayed = FixedCount >= 0 ? FixedCount : realCount + Offset;
            return Math.Max(1, displayed);
        }

        [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.UpdateMemberCountMetadata))]
        [HarmonyPrefix]
        private static bool PrefixUpdateMemberCountMetadata(SteamLobbyManager __instance, int count)
        {
            if (!Engine.EnabledOf(typeof(LobbyDisplayCountFeature)))
                return true;

            try
            {
                if (__instance.LobbyId == CSteamID.Nil)
                    return false;

                int displayed = DisplayCount(count);
                Traverse.Create(__instance).Field("_desiredMemberCount").SetValue(displayed);
                if (__instance.IsHost)
                    SteamMatchmaking.SetLobbyData(__instance.LobbyId, "members", displayed.ToString());
                Log.Info(Engine.SectionOf(typeof(LobbyDisplayCountFeature)),
                    $"对外显示人数 real={count} → displayed={displayed}");
                return false;
            }
            catch (global::System.Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(LobbyDisplayCountFeature)), ex, "改写显示人数失败");
                return true;
            }
        }
    }
}
