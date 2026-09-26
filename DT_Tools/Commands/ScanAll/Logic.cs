using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace DT_Tools.Commands.ScanAll
{
    /// <summary>
    /// /scan_all 业务：本机身份守卫 + 可扫描设备筛选 + C_SCAN_DEVICE 群发。
    ///
    /// 原版客户端 DeviceBase.Scan 要求本地物理接触并启动 2 秒扫描条，完成后才发
    /// C_SCAN_DEVICE{DeviceId}；服务端 DeviceManager.Scan → device.Scan(player)
    /// 全程无距离 / 阶段 / 颜色 / 存活检查，仅校验 !player.IsSpectator
    /// （0.1.15b DeviceManager.cs:142-144 / Server.Game/Device.cs:89）。
    /// 故直接对每个可扫描 ID 发包即可一次性拿回全部线索（S_SCAN_DEVICE /
    /// S_SCAN_CORPSE / S_SCAN_ARMORY）。
    ///
    /// 设备筛选：Bubble == -1 是设备在 StartDetective 时被标记为「有线索可扫描」的状态
    /// （0.1.15b：Device.cs:298-311 ClueList.Count &gt; 0 → -1；Corpse.cs:490-496
    /// !IsBombCorpse → -1；Armory.cs:277-284 StateList[6] != 0 → -1）。
    /// 三类同源，统一按 Bubble == -1 过滤即可覆盖设备 / 尸体 / 武器架全部线索源。
    /// 已扫描的设备 Bubble 被 SetBubble(0) 置 0，自然不再重复发包。
    /// </summary>
    internal static class ScanAllLogic
    {
        /// <summary>
        /// 本机已进入对局（MyPlayer 已生成、到 Host 的网络链路存在）且设备缓存就绪。
        /// 失败返回 false（提示与错误码已给出）。
        /// </summary>
        public static bool TryGetLocalContext(out DeviceManager device, out string code, out string text)
        {
            device = null;
            var my = Managers.Player?.MyPlayer;
            if (my == null || my.PrivateInfo == null)
            {
                code = "not in game";
                text = "本机玩家尚未进入对局（大厅/加载中不可用）。";
                return false;
            }
            if (Managers.Network == null || Managers.Network.GameServer == null)
            {
                code = "not in game";
                text = "未连接到 Host（GameServer 链路为空），无法发送 C_ 包。";
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
        /// </summary>
        public static void Execute(DeviceManager device, List<DeviceBase> targets)
        {
            foreach (var d in targets)
            {
                Managers.Network.GameServer.Send(new C_SCAN_DEVICE { DeviceId = d.ID });
                Managers.Game.ScannedDeviceSet.Add(d.ID);
                d.SetBubble(0);
            }
        }
    }
}
