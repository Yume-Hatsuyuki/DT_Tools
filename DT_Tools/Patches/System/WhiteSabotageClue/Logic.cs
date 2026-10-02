using System.Linq;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.WhiteSabotageClue
{
    /// <summary>
    /// 白方销毁证据执行序列（放行前缀见 Patch.DestroyEvidence）：
    /// 逐句镜像 Server.Game/Device.DestroyEvidence（0.1.16b Server.Game/Device.cs:222-260），
    /// 差异点仅一处——痕迹记录真实玩家身份而非固定假身份（黑方 66613 / 黑幕 11037，
    /// Device.cs:244-250）。
    /// </summary>
    internal static class WhiteSabotageClueLogic
    {
        /// <summary>
        /// 白方销毁证据：逐句镜像 Server.Game/Device.DestroyEvidence 的销毁序列
        /// （0.1.16b Server.Game/Device.cs:222-260：30 秒冷却 → 标记既有证据链 →
        /// 新增一条销毁痕迹），差异点仅一处——痕迹记录真实玩家身份（原版按行为人
        /// 颜色记固定假身份：黑方 66613 / 黑幕 11037，Device.cs:244-250）。
        /// 与 Game/SabotageClue.AddClue 的 30 秒窗口去重不同：原版销毁不走窗口去重，
        /// 每次销毁都新增一条；也不发 S_LEFT_CLUE（原版没有）。OnEvidenceDestroyed 为
        /// protected virtual（Device.cs:263），经反射调用以保留子类覆写。
        /// </summary>
        public static void DestroyEvidence(Device device, Server.Game.Player player)
        {
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
            }

            var trace = new PropositionInfo
            {
                PlayerId = player.PublicInfo.PlayerId,
                Time = TimeManager.Instance.SurviveTime,
                RoomId = device.RoomID,
                WhatType = device.DeviceData.WhatType,
            };
            device.ClueList.Add(trace);
            GameRoom.Instance.Broadcast(new S_ADD_CLUE
            {
                DeviceId = device.ID,
                Info = trace.Clone(),
            });

            Log.Info<WhiteSabotageClueFeature>(
                $"白方销毁证据留痕：{player.Name}(pid={player.PublicInfo.PlayerId}) 设备={device.ID} 房间={device.RoomID}");
            AccessTools.Method(typeof(Device), "OnEvidenceDestroyed")?.Invoke(device, null);
        }
    }
}
