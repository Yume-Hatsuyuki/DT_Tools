using System.Linq;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.WhiteSabotageClue
{
    /// <summary>
    /// 白方销毁证据执行序列（放行前缀见 Patch.DestroyEvidence）：
    /// 逐句镜像 Server.Game/Device.DestroyEvidence（0.1.18c Server.Game/Device.cs:219-268），
    /// 差异点仅一处——痕迹记录真实玩家身份而非固定假身份（黑方 66613 / 黑幕 11038，
    /// Device.cs:256）。
    /// </summary>
    internal static class WhiteSabotageClueLogic
    {
        /// <summary>
        /// 白方销毁证据：逐句镜像 Server.Game/Device.DestroyEvidence 的销毁序列
        /// （0.1.18c Server.Game/Device.cs:219-268：无可销毁线索前置校验 → 30 秒冷却 →
        /// 标记既有证据链并收集销毁回执 → 新增一条销毁痕迹 → 发送 S_LEFT_CLUE{RoleFace:true,
        /// DestroyedList}），差异点仅一处——痕迹记录真实玩家身份（原版按行为人
        /// 颜色记固定假身份：黑方 66613 / 黑幕 11038，Device.cs:256）。
        /// </summary>
        /// <remarks>
        /// 与 Game/SabotageClue.AddClue 的 30 秒窗口去重不同：原版销毁不走窗口去重，
        /// 每次销毁都新增一条；0.1.18c 起销毁者会收到带 DestroyedList 的 S_LEFT_CLUE
        /// 回执以播放销毁演出。OnEvidenceDestroyed 为 protected virtual（Device.cs:270），
        /// 经反射调用以保留子类覆写。
        /// </remarks>
        /// <param name="device">要销毁证据的设备。</param>
        /// <param name="player">执行销毁的白方玩家。</param>
        public static void DestroyEvidence(Device device, Server.Game.Player player)
        {
            // 0.1.18c 原版门禁：设备上无可销毁的普通线索（非销毁证据痕迹）时静默失败
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
