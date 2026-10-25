using System;
using System.Linq;
using Data;
using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.BlackKillNotify
{
    internal static class BlackKillNotifyLogic
    {
        private const float TargetFreshSeconds = 5f;

        private const int ChatDeviceFreeState = 0;

        private static bool _hasRemain;

        private static int _lastRemain;

        private static int _killTargetPid = -1;

        private static float _killTargetRealtime = float.NegativeInfinity;

        public static void RecordKillTarget(int targetPid)
        {
            _killTargetPid = targetPid;
            _killTargetRealtime = Time.unscaledTime;
        }

        public static void OnRemainKillApplied()
        {
            int remain = Managers.Game != null ? Managers.Game.RemainKill : 0;
            bool decreased = _hasRemain && remain < _lastRemain;
            _lastRemain = remain;
            _hasRemain = true;
            if (!decreased
                || !Engine.Enabled<BlackKillNotifyFeature>()
                || !BlackKillNotifyFeature.ClientSide)
                return;
            NotifyClient(remain);
        }

        private static void NotifyClient(int remain)
        {
            try
            {
                if (Managers.Game == null || Managers.Game.State != EGameState.Survive || !Managers.Game.IsAlive)
                    return;
                MyPlayer me = Managers.Player != null ? Managers.Player.MyPlayer : null;
                if (me == null || me.Color != EPlayerColor.Black)
                    return;
                if (_killTargetPid < 0 || Time.unscaledTime - _killTargetRealtime > TargetFreshSeconds)
                {
                    Log.Debug<BlackKillNotifyFeature>("击杀目标记录过期或缺失，放弃通报");
                    return;
                }
                if (Managers.Voice == null || !Managers.Voice.IsSecretChatReady)
                {
                    Log.Debug<BlackKillNotifyFeature>("秘密通话冷却中，跳过（7 秒内连杀的第二条被原版冷却拦下）");
                    return;
                }
                if (Managers.Network == null || Managers.Network.GameServer == null)
                    return;

                string character = ResolveCharacter(_killTargetPid, out PosInfo victimPos);
                string room = RoomLabel.FromPos(victimPos ?? me.PublicInfo?.Pos).localized;
                string message = Compose(Managers.Game.SurvivalTime, room, character, remain);

                DeviceBase device = PickFreeChatDevice(me);
                if (device == null)
                {
                    Log.Debug<BlackKillNotifyFeature>("无空闲打字机，放弃通报");
                    return;
                }

                Managers.Network.GameServer.Send(new C_INTERACT_CHATDEVICE { DeviceId = device.ID });
                Managers.Voice.SendSecretChatMessage(device.ID, message);
                Managers.Network.GameServer.Send(new C_HANDLE_CHATDEVICE { DeviceId = device.ID });
                Log.Info<BlackKillNotifyFeature>(
                    $"击杀通报已发送 device={device.ID} victimPid={_killTargetPid} remain={remain} len={message.Length}");
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(BlackKillNotifyFeature)), ex, "击杀通报失败");
            }
            finally
            {
                _killTargetPid = -1;
            }
        }

        public static void NotifyFromHost(Server.Game.Player victim, Server.Game.Player killer)
        {
            try
            {
                if (killer == null || killer.Color != EPlayerColor.Black)
                    return;
                Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
                if (room == null || room.IsMigrating)
                    return;

                int time = Server.Game.TimeManager.Instance.SurviveTime;
                string message = Compose(
                    time,
                    ResolveRoomName(victim, killer),
                    ResolveCharacterName(victim),
                    killer.RemainKill);

                var packet = new S_CHAT_MESSAGE
                {
                    Type = EChatType.SecretChat,
                    Text = message,
                    PlayerId = 0,
                    DeviceId = NearestChatDeviceTo(victim),
                    Time = time,
                    IsDead = false,
                    Seq = room.NextSecretChatSeq()
                };
                room.RecordSecretChat(packet);
                Server.Game.Replicator.Secrets(packet);
                Log.Info<BlackKillNotifyFeature>(
                    $"房主击杀通报已直发 killer pid={killer.PublicInfo.PlayerId} victim pid={victim.PublicInfo.PlayerId}"
                    + $" device={packet.DeviceId} remain={killer.RemainKill} len={message.Length}");
            }
            catch (Exception ex)
            {
                Log.Error(Engine.SectionOf(typeof(BlackKillNotifyFeature)), ex, "房主击杀通报失败");
            }
        }

        private static string Compose(int surviveTime, string room, string character, int remain)
            => $"{Util.FormatTimeSentence(surviveTime)}"
               + $"\n黑方在【{room}】处击杀了【{character}】，剩余【{remain}】攻击次数。";

        private static string ResolveCharacter(int pid, out PosInfo pos)
        {
            pos = null;
            Player victim = Managers.Player != null ? Managers.Player.GetPlayerCache(pid) : null;
            if (victim == null)
                return "未知角色";
            pos = victim.PublicInfo?.Pos;
            if (victim.PublicInfo != null
                && Managers.Data != null
                && Managers.Data.CharacterDic != null
                && Managers.Data.CharacterDic.TryGetValue(victim.PublicInfo.CharacterId, out CharacterData data)
                && !string.IsNullOrEmpty(data.Name))
                return data.Name;
            return string.IsNullOrEmpty(victim.DisplayName) ? "未知角色" : victim.DisplayName;
        }

        private static string ResolveRoomName(Server.Game.Player victim, Server.Game.Player killer)
        {
            var area = victim.CurrentArea ?? killer.CurrentArea;
            if (area == null || area.Data == null)
                return "未知地点";
            return Managers.GetText(Enum.GetName(typeof(ERoomType), area.Data.Type));
        }

        private static string ResolveCharacterName(Server.Game.Player victim)
        {
            string name = victim.Data?.Name;
            if (string.IsNullOrEmpty(name))
                name = victim.Name;
            return string.IsNullOrEmpty(name) ? "未知角色" : name;
        }

        private static int NearestChatDeviceTo(Server.Game.Player victim)
        {
            Server.Game.DeviceManager dm = Server.Game.DeviceManager.Instance;
            PosInfo pos = victim.PublicInfo?.Pos;
            if (dm == null || pos == null)
                return 0;

            Server.Game.Device best = null;
            float bestDist = float.MaxValue;
            foreach (Server.Game.Device device in dm.Objects)
            {
                if (device == null || device.DeviceType != EDeviceType.ChatDevice)
                    continue;
                PosInfo devicePos = device.DeviceData?.Pos;
                if (devicePos == null)
                    continue;
                float dx = devicePos.X - pos.X;
                float dy = devicePos.Y - pos.Y;
                float dist = dx * dx + dy * dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = device;
                }
            }
            return best?.ID ?? 0;
        }

        private static DeviceBase PickFreeChatDevice(MyPlayer me)
        {
            Vector3 myPos = me.transform.position;
            return Devices.AllOf(EDeviceType.ChatDevice)
                .Where(d => d != null && d.DeviceState == ChatDeviceFreeState)
                .OrderBy(d => (d.transform.position - myPos).sqrMagnitude)
                .FirstOrDefault();
        }
    }
}
