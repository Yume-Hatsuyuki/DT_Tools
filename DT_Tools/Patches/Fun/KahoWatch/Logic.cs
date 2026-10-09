using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.KahoWatch
{
    /// <summary>KAHO 监视判定与提示发送（服务端进程内共享状态）。</summary>
    internal static class KahoWatchLogic
    {
        /// <summary>提示冷却记录：key = watcherId * 1000 + actionType（0 拔刀 / 1 拆电闸 / 2 刀人）。</summary>
        private static readonly Dictionary<int, int> _lastNotifyMs = new Dictionary<int, int>();

        /// <summary>查找正在监视 target 的 KAHO（KAHO 角色且 TracePlayers 包含 target），无则返回 null。</summary>
        public static Server.Game.Player FindWatcher(Server.Game.Player target)
        {
            if (target == null) return null;
            var room = Server.Game.GameRoom.Instance;
            if (room == null) return null;

            foreach (var p in room.Players)
            {
                if (p == null || p == target || p.Data == null) continue;
                if (p.Data.Type == ECharacterType.Kaho && p.TracePlayers.Contains(target))
                    return p;
            }
            return null;
        }

        /// <summary>给 KAHO 发私聊提示（带冷却；Text 为具体动作描述）。</summary>
        public static void Notify(Server.Game.Player watcher, Server.Game.Player actor, int actionType, string actionText)
        {
            if (watcher == null || actor == null || watcher.Session == null) return;

            int key = watcher.PublicInfo.PlayerId * 1000 + actionType;
            int now = (int)(UnityEngine.Time.realtimeSinceStartup * 1000f);
            if (_lastNotifyMs.TryGetValue(key, out int last))
            {
                if (now - last < KahoWatchFeature.CooldownMs)
                    return;
            }
            _lastNotifyMs[key] = (int)now;

            watcher.Session.Send(new S_CHAT_MESSAGE
            {
                Type = EChatType.NormalChat,
                Text = $"[Kaho监视] {actor.Name} {actionText}",
                PlayerId = 0
            });
        }
    }
}
