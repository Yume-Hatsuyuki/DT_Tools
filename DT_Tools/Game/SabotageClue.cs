using System.Linq;
using Protocol;
using Server.Game;

namespace DT_Tools.Game
{
    /// <summary>
    /// 破坏留痕写入（Game 层共享）：以真实玩家身份在设备上写入一条线索（PropositionInfo），侦探扫描设备即可在平板上看到是谁在破坏。
    /// </summary>
    /// <remarks>
    /// 原版线索只记固定假身份：拉电闸固定记黑幕 11037（0.1.16b Server.Game/Fusebox.cs:127（待核对）），销毁证据按行为人颜色记 66613 / 11038（0.1.18c Server.Game/Device.cs:258-263）。
    /// 调用方：WhiteSabotageClue（拉电闸留痕）。锁门不留痕（原版锁门本就无证据；白方锁门留痕试验已移除：门的 RoomID 为 0，写入线索会让平板重建报 "0 is not exist room"）。
    /// 逐句镜像 Device.RecordLastUsingPlayer（0.1.18c Server.Game/Device.cs:160-215），含 WhatType==WNone 过滤（:162）：Survive 阶段、30 秒粒度窗口内同设备同玩家只刷新时间（S_REMOVE_CLUE + S_ADD_CLUE）；新线索 S_ADD_CLUE 广播（非房主客户端经 ClueMirror 自动同步，侦探扫描经 S_SCAN_DEVICE 带出），行为人收 S_LEFT_CLUE 回执。0.1.18c 起 PropositionInfo 不再含 WhatType 字段。
    /// </remarks>
    internal static class SabotageClue
    {
        // Player 必须全限定：全局命名空间有客户端 Player 类，裸名与 using 导入歧义
        public static void AddClue(Device device, Server.Game.Player player)
        {
            if (device?.DeviceInfo == null || player == null)
                return;
            if (GameRoom.Instance == null || GameRoom.Instance.State != EGameState.Survive)
                return;
            if (device.DeviceData == null || device.DeviceData.WhatType == EWhatType.WNone)
                return;   // 与原版一致（RecordLastUsingPlayer :162）：无有效 WhatType 不留痕

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
