using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DT_Tools.Commands;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.ScanAll
{
    /// <summary>
    /// /scan_all 业务：本机身份守卫 + 可扫描设备筛选 + C_SCAN_DEVICE 群发（间隔由命令参数指定，0=同帧发完）。
    ///
    /// 原版客户端 DeviceBase.Scan 要求本地物理接触并启动 2 秒扫描条，完成后才发
    /// C_SCAN_DEVICE{DeviceId}；服务端 DeviceManager.Scan → device.Scan(player)
    /// 全程无距离 / 阶段 / 颜色 / 存活检查，仅校验 !player.IsSpectator
    /// （0.1.16b Server.Game/DeviceManager.cs:142-144 / Server.Game/Device.cs:89）。
    /// 故直接对每个可扫描 ID 发包即可一次性拿回全部线索（S_SCAN_DEVICE /
    /// S_SCAN_CORPSE / S_SCAN_ARMORY）。
    ///
    /// 设备筛选：Bubble == -1 是设备在 StartDetective 时被标记为「有线索可扫描」的状态
    /// （0.1.16b：Device.cs:298-311 ClueList.Count &gt; 0 → -1；Server.Game/Corpse.cs:490-496
    /// !IsBombCorpse → -1；Server.Game/Armory.cs:277-284 StateList[6] != 0 → -1）。
    /// 三类同源，统一按 Bubble == -1 过滤即可覆盖设备 / 尸体 / 武器架全部线索源。
    /// 已扫描的设备 Bubble 被 SetBubble(0) 置 0，自然不再重复发包。
    /// </summary>
    internal static class ScanAllLogic
    {
        /// <summary>
        /// 本机已进入对局（MyPlayer 已生成、到 Host 的网络链路存在）且设备缓存就绪。
        /// 失败返回 false（提示与错误码已给出）。身份校验统一走 Game.LocalPlayer。
        /// </summary>
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

        /// <summary>观战不可扫描（服务端 DeviceManager.Scan 拒绝 IsSpectator）。</summary>
        public static bool IsSpectator => Managers.Game == null || Managers.Game.IsSpectator;

        /// <summary>收集所有 Bubble == -1 的可扫描设备，按 ID 升序。</summary>
        public static List<DeviceBase> CollectScannable(DeviceManager device)
            => device.Cache.Values
                .Where(d => d != null && d.Info != null && d.Info.Bubble == -1)
                .OrderBy(d => d.ID)
                .ToList();

        /// <summary>
        /// 逐个发包并同步本地状态（标记已扫描 + 清除泡泡 UI，与原版 DeviceBase.Scan 一致）。
        /// 服务端无成功回执：线索经 S_SCAN_DEVICE / S_SCAN_CORPSE / S_SCAN_ARMORY 推送。
        /// 发包间隔由命令参数给出：&gt;0 分帧；&lt;=0 时不 yield、协程体同步跑完，
        /// 全部 C_SCAN_DEVICE 同帧发出——服务端只是同帧顺序执行数十次轻量扫描+广播
        /// （Scan 无限流、JobSerializer.Flush 整队列清空，依据见文件头），无额外风险面。
        /// </summary>
        // 间隔=0 是真正的"同帧一次发完"：WaitForSeconds(0) 只是每帧一包，不等于同帧发完。
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

        /// <summary>启动发送协程（挂 Core 常驻协程宿主，不依赖命令上下文存活）。</summary>
        public static void StartPacketsCoroutine(List<DeviceBase> targets, float interval, CommandContext ctx)
        {
            Core.CoroutineHost.Start(SendPackets(targets, interval, ctx));
        }
    }
}
