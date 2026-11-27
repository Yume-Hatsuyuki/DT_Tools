// 作用：实现黑方击杀通报的运行时逻辑；不负责补丁挂载与配置声明。
using System;
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
                if (Managers.Voice == null)
                    return;
                if (Managers.Network == null || Managers.Network.GameServer == null)
                    return;

                string character = ResolveCharacter(_killTargetPid, out PosInfo victimPos);
                string room = RoomLabel.FromPos(victimPos ?? me.PublicInfo?.Pos).localized;
                string message = Compose(Managers.Game.SurvivalTime, room, character, remain);

                Managers.Voice.SendRadioMessage(message);
                Log.Info<BlackKillNotifyFeature>(
                    $"击杀通报已发送 victimPid={_killTargetPid} remain={remain} len={message.Length}");
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
                if (killer == null || killer.Color != EPlayerColor.Black || !killer.IsAlive)
                    return;
                Server.Game.GameRoom room = Server.Game.GameRoom.Instance;
                if (room == null || room.IsMigrating)
                    return;
                if (room.State != EGameState.Survive && room.State != EGameState.Detective)
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
                    PlayerId = killer.PublicInfo.PlayerId
                };
                EPlayerColor target = killer.Color == EPlayerColor.Dark ? EPlayerColor.Black : EPlayerColor.Dark;
                if (killer.Session != null)
                    killer.Session.Send(packet);
                foreach (Server.Game.Player player in room.Players)
                {
                    if (player == killer || player == null || player.IsDummy || !player.IsAlive
                        || player.Color != target || player.Session == null)
                        continue;
                    player.Session.Send(packet);
                }
                Log.Info<BlackKillNotifyFeature>(
                    $"房主击杀通报已发送 killer pid={killer.PublicInfo.PlayerId} victim pid={victim.PublicInfo.PlayerId}"
                    + $" remain={killer.RemainKill} len={message.Length}");
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
    }
}
