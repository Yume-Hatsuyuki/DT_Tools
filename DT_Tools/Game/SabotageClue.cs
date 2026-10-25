using System.Linq;
using Protocol;
using Server.Game;

namespace DT_Tools.Game
{
    internal static class SabotageClue
    {
        public static void AddClue(Device device, Server.Game.Player player)
        {
            if (device?.DeviceInfo == null || player == null)
                return;
            if (GameRoom.Instance == null || GameRoom.Instance.State != EGameState.Survive)
                return;
            if (device.DeviceData == null || device.DeviceData.WhatType == EWhatType.WNone)
                return;

            int playerId = player.PublicInfo.PlayerId;
            int surviveTime = TimeManager.Instance.SurviveTime;
            int limitTime = surviveTime / 30 * 30;
            var existing = device.ClueList.FirstOrDefault(
                x => x.Time >= limitTime && x.PlayerId == playerId && !x.Destroyed);
            if (existing == null)
            {
                var info = new PropositionInfo
                {
                    PlayerId = playerId,
                    Time = surviveTime,
                    RoomId = device.RoomID,
                };
                device.ClueList.Add(info);
                GameRoom.Instance.Broadcast(new S_ADD_CLUE
                {
                    DeviceId = device.ID,
                    Info = info.Clone(),
                });
            }
            else
            {
                var stale = existing.Clone();
                device.ClueList.Remove(existing);
                existing.Time = surviveTime;
                device.ClueList.Add(existing);
                GameRoom.Instance.Broadcast(new S_REMOVE_CLUE
                {
                    DeviceId = device.ID,
                    Info = stale,
                });
                GameRoom.Instance.Broadcast(new S_ADD_CLUE
                {
                    DeviceId = device.ID,
                    Info = existing.Clone(),
                });
            }
            player.Session.Send(new S_LEFT_CLUE());
        }
    }
}
