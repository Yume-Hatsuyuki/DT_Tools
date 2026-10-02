using System;
using DummyClient;
using HarmonyLib;
using Steamworks;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.LobbyDisplayCount
{
    /// <summary>
    /// 自定义大厅显示人数（房主权威）：对外（Steam 大厅列表）显示的人数可与房间
    /// 真实人数不同——按偏移量或固定值伪装。对外显示走 Steam Lobby metadata
    /// "members"（房主 UpdateMemberCountMetadata 发布，客户端 OnLobbyList 读它填
    /// LobbyListEntry.Cur 再经 UI_RoomSubItem 渲染），改它不影响任何游戏内判定。
    ///
    /// 满员判定（GameRoom.HandleEnterPlayer 的 RoomMemberCountForMetadata() >= 上限
    /// 与 ObjectUtils.HasFreeSeat()）走原版路径，本功能一概不碰——因此房间达到最大
    /// 人数时依旧拒绝加入。配合已有功能：
    ///   - SpectatorJoin（满房观战）：其 Prefix 以 Priority.First 整替 HandleEnterPlayer，
    ///     满房观战窗口仍按观战逻辑放行，与本功能（只改 metadata）互不干扰；
    ///   - LobbyMaxPlayers（超过人数上限）：只调 Steam Lobby 容器上限，显示值独立可配。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "自定义大厅显示人数：对外（Steam 大厅列表）显示人数可与房间真实人数不同（偏移或固定值）。真实满员判定不变——达到最大人数依旧拒绝加入；与 SpectatorJoin（满房观战）、LobbyMaxPlayers（超上限）天然兼容。仅房主生效。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "Doubao")]
    public sealed class LobbyDisplayCountFeature
    {
        [Config("显示人数偏移：对外显示人数 = 房间真实人数 + 偏移（可为负）。结果钳制下限 1（Steam 大厅列表读不到 <=0 的值会回退显示真实 Steam 成员数）。", Min = -64, Max = 64)]
        public static int Offset = 0;

        [Config("固定显示人数：>= 0 时直接对外显示该值（忽略偏移）；-1 表示不启用固定值。同样钳制下限 1。", Min = -1, Max = 64)]
        public static int FixedCount = -1;

        /// <summary>对外显示人数：固定值优先，否则 真实人数 + 偏移；下限 1。</summary>
        private static int DisplayCount(int realCount)
        {
            int displayed = FixedCount >= 0 ? FixedCount : realCount + Offset;
            return Math.Max(1, displayed);
        }

        /// <summary>
        /// 拦截 SteamLobbyManager.UpdateMemberCountMetadata（0.1.15b DummyClient/
        /// SteamLobbyManager.cs:443-447）：只改写对外发布到 Steam Lobby "members"
        /// 的值与 _desiredMemberCount 字段（RepublishOwnedMetadata 重发时保持一致），
        /// 其余逻辑（EnterPlayer/LeavePlayer 调用点、满员判定、加入决策）全部走原版。
        /// 原方法体很短（LobbyId==Nil 早退 + 写字段 + SetLobbyData），此处等价复制，
        /// 仅把 count 换成显示值。
        /// </summary>
        [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.UpdateMemberCountMetadata))]
        [HarmonyPrefix]
        private static bool PrefixUpdateMemberCountMetadata(SteamLobbyManager __instance, int count)
        {
            if (!Engine.EnabledOf(typeof(LobbyDisplayCountFeature)))
                return true;

            try
            {
                if (__instance.LobbyId == CSteamID.Nil)
                    return false; // 原逻辑在此分支同样不做事

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
                Log.Error(Engine.SectionOf(typeof(LobbyDisplayCountFeature)), "改写显示人数失败: " + ex.Message);
                return true; // 异常时放行原逻辑，避免破坏进房/离房人数发布
            }
        }
    }
}
