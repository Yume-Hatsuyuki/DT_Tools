using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.ReportCorpse
{
    internal static class ReportCorpseLogic
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
            code = null;
            text = null;
            return true;
        }

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

        public static DeviceBase FindCorpse(DeviceManager device, int corpseId)
        {
            if (device == null) return null;
            return device.Cache.TryGetValue(corpseId, out var d)
                   && d != null
                   && d.DeviceType == EDeviceType.Corpse
                ? d
                : null;
        }

        public static string GetBlockReason(DeviceManager device, DeviceBase corpse)
        {
            var states = corpse?.Info?.StateList;
            if (states == null || states.Count < 5) return "状态字段缺失";
            if (states[3] != 0) return "炸弹尸体";
            if (states[4] != 0) return "已隐藏";
            if (device != null && device.IsCorpseTrickLocked(corpse.ID)) return "被致命诡计锁定";
            return null;
        }

        public static List<DeviceBase> ListCorpses(DeviceManager device)
        {
            if (device == null) return new List<DeviceBase>();
            return device.Cache.Values
                .Where(d => d != null && d.DeviceType == EDeviceType.Corpse && d.Info?.StateList != null && d.Info.StateList.Count >= 5)
                .OrderBy(d => d.ID)
                .ToList();
        }

        public static string FindPlayerName(int playerId)
        {
            var dict = Managers.Player?.Players;
            if (dict == null || !dict.TryGetValue(playerId, out var p) || p == null)
                return "(未命名/已退出)";
            return p.Name ?? "(未命名/已退出)";
        }
    }
}
