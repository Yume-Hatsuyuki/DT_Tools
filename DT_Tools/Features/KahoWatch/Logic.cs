using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.KahoWatch
{
    internal static class KahoWatchLogic
    {
        private static readonly Dictionary<int, int> _lastNotifyMs = new Dictionary<int, int>();

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
