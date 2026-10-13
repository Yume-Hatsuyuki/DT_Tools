using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.ReportCorpse
{
    /// <summary>
    /// /report 业务：本机身份守卫 + 尸体筛选与可报警判定 + C_INTERACT_CORPSE 报警。
    /// </summary>
    /// <remarks>
    /// 原版 Corpse.Interact（0.1.16b Server.Game/Corpse.cs:262-270（待核对））只校验 !IsHidden &amp;&amp; !TrickLocked &amp;&amp; !_isReport &amp;&amp; !DiscoverdDone，无距离/颜色/存活/观战检查，可在任意位置发包触发 EndSurvival 进入 Detective。尸体 DeviceId = 死者 PlayerId（Server.Game/Corpse.cs:190/:227）。
    /// 客户端可见状态：StateList[3]!=0 → 炸弹尸体（UI 同样禁报）；StateList[4]!=0 → HideDeviceId!=0，IsHidden（藏在容器里）；IsCorpseTrickLocked → 被致命诡计锁定。
    /// _isReport / DiscoverdDone 为服务端私有字段不可见；二者为 true 时 EndSurvival 已执行，报警会被 RequireSurvive 拦截。
    /// </remarks>
    internal static class ReportCorpseLogic
    {
        /// <summary>
        /// 本机已进入对局（MyPlayer 已生成、到 Host 的网络链路存在）。
        /// </summary>
        /// <remarks>
        /// 身份校验统一走 Game.LocalPlayer。
        /// </remarks>
        /// <param name="device">本机设备管理器；失败时为 null。</param>
        /// <param name="code">失败时的错误码。</param>
        /// <param name="text">失败时的提示文字。</param>
        /// <returns>本机已进入对局且设备缓存就绪时为 true。</returns>
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
            code = null;
            text = null;
            return true;
        }

        /// <summary>
        /// 报警只在生存阶段有意义（已开会/未开局都拒）。
        /// </summary>
        /// <param name="code">拒绝时的错误码。</param>
        /// <param name="text">拒绝时的提示文字。</param>
        /// <returns>当前为生存阶段时为 true。</returns>
        public static bool RequireSurvive(out string code, out string text)
        {
            if (!LocalPlayer.IsSurvive)
            {
                code = "invalid state";
                text = $"仅生存阶段可用，当前状态: {LocalPlayer.StateText}。";
                return false;
            }
            code = null;
            text = null;
            return true;
        }

        /// <summary>
        /// 从本机设备缓存取尸体（DeviceType==Corpse）。
        /// </summary>
        /// <param name="device">本机设备管理器。</param>
        /// <param name="corpseId">尸体 ID，与死者的 PlayerId 相同。</param>
        /// <returns>尸体设备；找不到时为 null。</returns>
        public static DeviceBase FindCorpse(DeviceManager device, int corpseId)
        {
            if (device == null) return null;
            return device.Cache.TryGetValue(corpseId, out var d)
                   && d != null
                   && d.DeviceType == EDeviceType.Corpse
                ? d
                : null;
        }

        /// <summary>
        /// 返回 null 表示可报警；否则返回不可报警的中文原因
        /// （与服务端 Corpse.Interact 的 4 项门禁对齐，仅客户端可见的 3 项）。
        /// </summary>
        /// <param name="device">本机设备管理器。</param>
        /// <param name="corpse">要检查的尸体。</param>
        /// <returns>可以报警时为 null；否则为不可报警的中文原因。</returns>
        public static string GetBlockReason(DeviceManager device, DeviceBase corpse)
        {
            var states = corpse?.Info?.StateList;
            if (states == null || states.Count < 5) return "状态字段缺失";
            if (states[3] != 0) return "炸弹尸体";
            if (states[4] != 0) return "已隐藏";
            if (device != null && device.IsCorpseTrickLocked(corpse.ID)) return "被致命诡计锁定";   // 0.1.17a DeviceManager.cs:499
            return null;
        }

        /// <summary>
        /// 列出场上所有尸体（StateList 完整的），按 ID 升序。
        /// </summary>
        /// <param name="device">本机设备管理器。</param>
        /// <returns>场上所有尸体，按 ID 升序排列。</returns>
        public static List<DeviceBase> ListCorpses(DeviceManager device)
        {
            if (device == null) return new List<DeviceBase>();
            return device.Cache.Values
                .Where(d => d != null && d.DeviceType == EDeviceType.Corpse && d.Info?.StateList != null && d.Info.StateList.Count >= 5)
                .OrderBy(d => d.ID)
                .ToList();
        }

        /// <summary>
        /// 从本机客户端玩家表查尸体对应死者名。
        /// </summary>
        /// <param name="playerId">死者的 PlayerId。</param>
        /// <returns>死者的名字。</returns>
        public static string FindPlayerName(int playerId)
        {
            var dict = Managers.Player?.Players;
            if (dict == null || !dict.TryGetValue(playerId, out var p) || p == null)
                return "(未命名/已退出)";
            return p.Name ?? "(未命名/已退出)";
        }
    }
}
