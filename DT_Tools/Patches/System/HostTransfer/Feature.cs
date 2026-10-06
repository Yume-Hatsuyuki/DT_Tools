using HarmonyLib;
using Protocol;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.HostTransfer
{
    /// <summary>
    /// 房主局内转让：客户端侧配合 transferhost 命令的房主标记同步。
    ///
    /// 原版客户端 PacketHandler.Handle_S_SET_HOST 只在 Lobby 阶段更新房主标记
    /// （BroadcastSceneEvent(SetHost) + SetPlayerLobbyState(Host)，
    /// 0.1.16b PacketHandler.cs:332-340）；局内转让时服务端广播的 S_SET_HOST
    /// 会被客户端忽略，UI 上的房主标记不会变化。
    /// 本补丁在原版处理之后补执行同样的标记更新（仅非 Lobby，避免重复），
    /// 让大厅与局内转让都能在客户端正确显示新房主。
    /// 服务端转让逻辑在 Commands/TransferHost（transferhost 命令），与本补丁解耦：
    /// 关闭本功能时服务端权限转让仍生效，只是局内房主标记不更新。
    /// PacketHandler 为 internal 类，采用字符串类型名反射打点（同 SpectatorJoin）。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        "房主局内转让：房主可通过 /transferhost #<playerId> 将房主身份转给其他玩家（大厅/局内均可），本功能让局内客户端同步更新房主标记。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Author = "合理")]
    public sealed class HostTransferFeature
    {
        /// <summary>
        /// PacketHandler 为 internal 类，按类型名字符串反射定位补丁目标
        /// （0.1.16b PacketHandler.cs:332 Handle_S_SET_HOST，无重载）。
        /// </summary>
        internal static global::System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_SET_HOST");

        [HarmonyPostfix]
        private static void Postfix(Packet packet)
        {
            if (!Engine.EnabledOf(typeof(HostTransferFeature)))
                return;
            // 原版已在 Lobby 处理，跳过避免重复广播
            if (Managers.Game.State == EGameState.Lobby)
                return;
            if (packet?.Pkt is not S_SET_HOST pkt || pkt.HostId == 0)
                return;

            try
            {
                Managers.Game.BroadcastSceneEvent(Define.ESceneEventType.SetHost, pkt.HostId);
                Managers.Player.SetPlayerLobbyState(pkt.HostId, Define.ELobbyState.Host);
                Log.Info(Engine.SectionOf(typeof(HostTransferFeature)),
                    $"局内房主已转让 → #{pkt.HostId}");
            }
            catch (global::System.Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(HostTransferFeature)), "局内房主标记同步失败: " + ex.Message);
            }
        }
    }
}
