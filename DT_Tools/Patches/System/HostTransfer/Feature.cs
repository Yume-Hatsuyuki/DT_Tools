using HarmonyLib;
using Protocol;
using Steamworks;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.HostTransfer
{
    /// <summary>
    /// 房主局内转让：客户端侧配合 transferhost 命令的房主标记同步。
    ///
    /// 原版客户端 PacketHandler.Handle_S_SET_HOST 只在 Lobby 阶段更新房主标记
    /// （BroadcastSceneEvent(SetHost) + SetPlayerLobbyState(Host)，
    /// 0.1.16a PacketHandler.cs:332-340）；局内转让时服务端广播的 S_SET_HOST
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
        /// 转让通知的自定义文本。完整显示格式：
        ///   #原房主 <此文本> #新房主 （仅房主权限/大厅owner）
        /// 例如默认值 → "#Aki 将房主转让给 #Yumi （仅房主权限）"。
        /// 服务端在 /transferhost 执行时广播给全员（大厅/裁判阶段聊天面板，
        /// 生存阶段密聊浮层）。
        /// </summary>
        [Config("房主转让通知文本（显示为：#原房主 <此文本> #新房主 （仅房主权限/大厅owner））。留空则用默认“将房主转让给”。")]
        public static string TransferText = "将房主转让给";

        [HarmonyPatch]
        private static class HostTransferPatch
        {
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

        // ── 受控 Steam 大厅 owner 转让：防"房主离开"误迁移 ──────────────────
        // /transferhost #<id> owner 会调用 SteamMatchmaking.SetLobbyOwner(新房主)，
        // 但游戏内置机制（NetworkManager.OnHostChanged）把"大厅 owner 变化"当作
        // "房主离开"处理：普通客户端会走 ComputeSuccessor/RerouteToNewHost/
        // OnBecomeNewHost 触发全员误迁移。本补丁在游戏房主（GameHostSteamId）
        // 仍在房间成员中时拦截该处理——此时 owner 变化属受控转让（或正常漂移），
        // 不迁移；游戏房主离开房间（不在成员）时仍走原版迁移逻辑。
        // 注意：游戏房主（H）进程自身 OnHostChanged 原版会"忽略"（IsHost 分支），
        // 本补丁同样拦截，行为等价。0.1.16a NetworkManager.cs:410 OnHostChanged。
        [HarmonyPatch]
        private static class OnHostChangedPatch
        {
            internal static global::System.Reflection.MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.TypeByName("NetworkManager"), "OnHostChanged");

            [HarmonyPrefix]
            private static bool Prefix(CSteamID newHost)
            {
                if (!Engine.EnabledOf(typeof(HostTransferFeature)))
                    return true;
                if (Managers.Network == null || Managers.Network.Lobby == null || !Managers.Network.Lobby.InLobby)
                    return true;

                var gameHost = Managers.Network.GameHostSteamId;
                if (newHost == gameHost)
                    return true; // 正常同步回游戏房主，走原版

                // 游戏房主仍在房间 → 受控转让/漂移：只同步认知（SteamLobbyManager
                // 已更新 HostSteamId），跳过原版迁移误判。
                if (Managers.Network.Lobby.GetMembers().Contains(gameHost))
                {
                    ulong gameHostId = gameHost.m_SteamID;
                    Log.Info(Engine.SectionOf(typeof(HostTransferFeature)),
                        $"大厅 owner 变化，游戏房主 {gameHostId} 仍在房间，跳过迁移（受控转让）");
                    return false;
                }
                return true;
            }
        }

        // ── 新房主代写大厅状态 ──────────────────────────────────────────────
        // Steam 限制：仅大厅 owner 可 SetLobbyData。受控转让大厅 owner 后，
        // 原游戏房主（服务器）的 SetLobbyState（state=ingame/lobby）会因非 owner
        // 而失败。本补丁让新房主（新 owner）在 owner 变化后重发一次当前大厅状态
        // 与成员数，保持对外可见状态正确。0.1.16a SteamLobbyManager.cs:457
        // RepublishOwnedMetadata（原版仅重发 members）。
        [HarmonyPatch]
        private static class NewOwnerRepublishPatch
        {
            internal static global::System.Reflection.MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.TypeByName("SteamLobbyManager"), "OnLobbyDataUpdate");

            [HarmonyPostfix]
            private static void Postfix()
            {
                if (!Engine.EnabledOf(typeof(HostTransferFeature)))
                    return;
                if (Managers.Network?.Lobby == null || !Managers.Network.Lobby.InLobby)
                    return;
                // 自己是大厅 owner 且不是游戏房主（受控转让产生的新 owner）
                if (Managers.Network.Lobby.IsHost && !Managers.Host.IsHost)
                {
                    SteamMatchmaking.SetLobbyData(Managers.Network.Lobby.LobbyId, "state",
                        Managers.Game.State == EGameState.Lobby ? "lobby" : "ingame");
                    Log.Info(Engine.SectionOf(typeof(HostTransferFeature)),
                        $"新房主代写大厅状态 state={(Managers.Game.State == EGameState.Lobby ? "lobby" : "ingame")}（owner 已转让，原房主无权写大厅数据）");
                }
            }
        }
    }
}
