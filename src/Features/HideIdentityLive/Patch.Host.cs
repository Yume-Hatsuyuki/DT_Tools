// 作用：房主侧识别重发的原生进房包并只更新隐藏身份；不改变首次进房判定与重入流程。
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.HideIdentityLive
{
    [HarmonyPatch(typeof(HostPacketHandler), nameof(HostPacketHandler.Handle_C_ENTER_GAME))]
    internal static class HideIdentityEnterPacketPatch
    {
        private static bool Prefix(IPacketSink session, Packet packet)
        {
            if (!Engine.Enabled<HideIdentityLiveFeature>())
                return true;

            HostPeerSession peer = session as HostPeerSession;
            C_ENTER_GAME pkt = packet.Pkt as C_ENTER_GAME;
            if (peer == null || pkt == null || peer.Player == null || peer.Player.IsDummy)
                return true;
            if (peer.Player.HideIdentity == pkt.HideIdentity)
                return true;

            peer.Player.HideIdentity = pkt.HideIdentity;
            GameRoom.Instance.BroadcastRoster();
            Log.Info<HideIdentityLiveFeature>(
                $"房间内更新隐藏身份 pid={peer.Player.PublicInfo.PlayerId} hide={pkt.HideIdentity}");
            return false;
        }
    }
}
