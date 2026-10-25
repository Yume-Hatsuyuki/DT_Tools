using HarmonyLib;
using Protocol;
using Steamworks;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.HostTransfer
{
    [HarmonyPatch]
    [PatchFeature(
        "房主局内转让：房主可通过 /transferhost #<playerId> 将房主身份转给其他玩家（大厅/局内均可），本功能让局内客户端同步更新房主标记。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        Tags = FeatureTag.SysHost | FeatureTag.SysNetwork,
        Author = "合理")]
    public sealed class HostTransferFeature
    {
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
                    Log.Error(Engine.SectionOf(typeof(HostTransferFeature)), ex, "局内房主标记同步失败");
                }
            }
        }

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
                    return true;

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
