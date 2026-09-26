using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.ReportCorpse
{
    /// <summary>
    /// /report 业务：本机身份守卫 + 尸体筛选与可报警判定 + C_INTERACT_CORPSE 报警。
    ///
    /// 原版 Corpse.Interact（0.1.15b Server.Game/Corpse.cs:262-270）仅校验
    /// !IsHidden && !TrickLocked && !_isReport && !DiscoverdDone，没有距离 / 颜色 /
    /// IsAlive / IsSpectator 检查，故可在地图任意位置直接发包触发 EndSurvival 进入
    /// Detective。尸体 DeviceId 与死者 PlayerId 相同（Corpse.cs:190/:227 构造函数
    /// base.ID = playerInfo.PlayerId）。
    ///
    /// 客户端可见的尸体状态字段（与 Server.Game/Corpse.cs 对齐）：
    ///   StateList[3] != 0 → IsBombCorpse（炸弹尸体，客户端 UI 同样禁报）
    ///   StateList[4] != 0 → HideDeviceId != 0，IsHidden（藏在池塘/通风管等容器）
    ///   IsCorpseTrickLocked → 被致命诡计锁定
    /// _isReport / DiscoverdDone 是服务端私有字段不可见；二者任一为 true 时
    /// EndSurvival 已执行、GameRoom.State != Survive，报警前会被 RequireSurvive 拦截。
    /// </summary>
    internal static class ReportCorpseLogic
    {
        /// <summary>本机已进入对局（MyPlayer 已生成、到 Host 的网络链路存在）。</summary>
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
            code = null;
            text = null;
            return true;
        }

        /// <summary>报警只在生存阶段有意义（已开会/未开局都拒）。</summary>
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

        /// <summary>从本机设备缓存取尸体（DeviceType==Corpse）。</summary>
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
        public static string GetBlockReason(DeviceManager device, DeviceBase corpse)
        {
            var states = corpse?.Info?.StateList;
            if (states == null || states.Count < 5) return "状态字段缺失";
            if (states[3] != 0) return "炸弹尸体";
            if (states[4] != 0) return "已隐藏";
            if (device != null && device.IsCorpseTrickLocked(corpse.ID)) return "被致命诡计锁定";   // 0.1.15b DeviceManager.cs:499
            return null;
        }

        /// <summary>列出场上所有尸体（StateList 完整的），按 ID 升序。</summary>
        public static List<DeviceBase> ListCorpses(DeviceManager device)
        {
            if (device == null) return new List<DeviceBase>();
            return device.Cache.Values
                .Where(d => d != null && d.DeviceType == EDeviceType.Corpse && d.Info?.StateList != null && d.Info.StateList.Count >= 5)
                .OrderBy(d => d.ID)
                .ToList();
        }

        /// <summary>从本机客户端玩家表查尸体对应死者名。</summary>
        public static string FindPlayerName(int playerId)
        {
            var dict = Managers.Player?.Players;
            if (dict == null || !dict.TryGetValue(playerId, out var p) || p == null)
                return "(未命名/已退出)";
            return p.Name ?? "(未命名/已退出)";
        }
    }
}
