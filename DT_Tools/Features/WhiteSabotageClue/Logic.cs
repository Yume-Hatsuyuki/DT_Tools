using System.Linq;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.WhiteSabotageClue
{
    internal static class WhiteSabotageClueLogic
    {
        public static void DestroyEvidence(Device device, Server.Game.Player player)
        {
            if (!device.ClueList.Any((PropositionInfo x) => !x.Destroyed && !Define.IsDestroyEvidenceClue(x)))
                return;

            player.CanDestroyEvidence = false;
            player.DestroyEvidenceCooltimeEndTick = TimeManager.Instance.SurviveTime + 30;
            TimeManager.Instance.PushSurvivalJob(30, delegate
            {
                player.CanDestroyEvidence = true;
            });
            player.Session.Send(new S_COOLTIME_DESTROY_EVIDENCE
            {
                Cooltime = 30
            });

            var leftClue = new S_LEFT_CLUE
            {
                RoleFace = true
            };
            foreach (PropositionInfo item in device.ClueList
                .Where((PropositionInfo x) => !x.Destroyed && !Define.IsDestroyEvidenceClue(x))
                .ToList())
            {
                GameRoom.Instance.Broadcast(new S_REMOVE_CLUE
                {
                    DeviceId = device.ID,
                    Info = item.Clone(),
                });
                item.Destroyed = true;
                GameRoom.Instance.Broadcast(new S_ADD_CLUE
                {
                    DeviceId = device.ID,
                    Info = item.Clone(),
                });
                leftClue.DestroyedList.Add(item.Clone());
            }

            var trace = new PropositionInfo
            {
                PlayerId = player.PublicInfo.PlayerId,
                Time = TimeManager.Instance.SurviveTime,
                RoomId = device.RoomID,
            };
            device.ClueList.Add(trace);
            GameRoom.Instance.Broadcast(new S_ADD_CLUE
            {
                DeviceId = device.ID,
                Info = trace.Clone(),
            });
            player.Session.Send(leftClue);

            Log.Info<WhiteSabotageClueFeature>(
                $"白方销毁证据留痕：{player.Name}(pid={player.PublicInfo.PlayerId}) 设备={device.ID} 房间={device.RoomID}");
            AccessTools.Method(typeof(Device), "OnEvidenceDestroyed")?.Invoke(device, null);
        }
    }
}
