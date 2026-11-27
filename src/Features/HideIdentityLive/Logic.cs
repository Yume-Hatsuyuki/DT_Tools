// 作用：把房间内的隐藏身份变更同步到房主与名册；不负责设置持久化与展示层。
using System;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Features.HideIdentityLive
{
    internal static class HideIdentitySync
    {
        private static readonly MethodInfo SendEnterPacketMethod =
            AccessTools.Method(typeof(NetworkManager), "SendEnterPacket");

        internal static void Apply(bool on)
        {
            if (Managers.Host != null && Managers.Host.IsHost)
            {
                ApplyAsHost(on);
                return;
            }

            ApplyAsGuest();
        }

        private static void ApplyAsHost(bool on)
        {
            try
            {
                GameRoom room = GameRoom.Instance;
                Server.Game.Player me = null;
                int myId = Managers.Player != null ? Managers.Player.MyPlayerID : -1;
                if (myId >= 0)
                    me = room.Players.Find(p => p.PublicInfo != null && p.PublicInfo.PlayerId == myId);
                if (me == null)
                    me = room.Host;
                if (me == null)
                {
                    Log.Warn<HideIdentityLiveFeature>("房主本地玩家尚未建立，隐藏身份未同步");
                    return;
                }

                me.HideIdentity = on;
                room.BroadcastRoster();
                Log.Info<HideIdentityLiveFeature>(
                    $"房主本机隐藏身份已广播 pid={me.PublicInfo.PlayerId} hide={on}");
            }
            catch (Exception ex)
            {
                Log.Warn<HideIdentityLiveFeature>(ex, "房主本机同步隐藏身份失败");
            }
        }

        private static void ApplyAsGuest()
        {
            if (Managers.Player == null || Managers.Player.MyPlayer == null)
            {
                Log.Warn<HideIdentityLiveFeature>("尚未完成进房，隐藏身份未同步");
                return;
            }
            if (SendEnterPacketMethod == null)
            {
                Log.Warn<HideIdentityLiveFeature>("未找到原生 C_ENTER_GAME 发送入口，隐藏身份未同步");
                return;
            }

            try
            {
                SendEnterPacketMethod.Invoke(Managers.Network, null);
                Log.Info<HideIdentityLiveFeature>("已重发原生进房包以同步隐藏身份");
            }
            catch (Exception ex)
            {
                Log.Warn<HideIdentityLiveFeature>(ex, "重发原生进房包失败");
            }
        }
    }
}
