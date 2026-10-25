using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.ScanAll
{
    internal static class ScanAllLogic
    {
        public static bool TryGetLocalContext(out DeviceManager device, out string code, out string text)
        {
            device = null;
            if (!LocalPlayer.TryGetPlayer(out _, out string identityError))
            {
                code = "not in game";
                text = identityError;
                return false;
            }

            device = Managers.Device;
            if (device == null || device.Cache == null || device.Cache.Count == 0)
            {
                code = "no device cache";
                text = "地图尚未加载或没有设备数据（请进入对局后再试）。";
                return false;
            }

            code = null;
            text = null;
            return true;
        }

        public static bool IsSpectator => Managers.Game == null || Managers.Game.IsSpectator;

        public static List<DeviceBase> CollectScannable(DeviceManager device)
            => device.Cache.Values
                .Where(d => d != null && d.Info != null && d.Info.Bubble == -1)
                .OrderBy(d => d.ID)
                .ToList();

        public static IEnumerator SendPackets(List<DeviceBase> targets, float interval, CommandContext ctx)
        {
            int sent = 0;
            foreach (var d in targets)
            {
                if (Managers.Network == null || Managers.Network.GameServer == null || Managers.Game == null)
                {
                    ctx.Warn($"【知晓一切】网络链路已断开，中止剩余发包（已发 {sent}/{targets.Count}）。");
                    yield break;
                }

                Managers.Network.GameServer.Send(new C_SCAN_DEVICE { DeviceId = d.ID });
                Managers.Game.ScannedDeviceSet.Add(d.ID);
                d.SetBubble(0);
                sent++;

                if (interval > 0f && sent < targets.Count)
                    yield return new WaitForSeconds(interval);
            }
        }

        public static void StartPacketsCoroutine(List<DeviceBase> targets, float interval, CommandContext ctx)
        {
            Core.CoroutineHost.Start(SendPackets(targets, interval, ctx));
        }
    }
}
