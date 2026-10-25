using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.ForceRinPick
{
    internal static class ForceRinPickLogic
    {
        private static readonly Dictionary<int, int> _forced = new Dictionary<int, int>();

        public static void Mark(int playerId, int characterDataId)
        {
            if (characterDataId <= 0) return;
            _forced[playerId] = characterDataId;
        }

        public static int Consume(int playerId)
        {
            if (_forced.TryGetValue(playerId, out int dataId))
            {
                _forced.Remove(playerId);
                return dataId;
            }
            return 0;
        }

        public static Server.Game.Player ResolveTarget(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return null;
            var room = Server.Game.GameRoom.Instance;
            if (room == null) return null;

            string trimmed = arg.Trim();
            if (int.TryParse(trimmed, out int id))
            {
                foreach (var p in room.Players)
                    if (p.PublicInfo.PlayerId == id)
                        return p;
                return null;
            }
            foreach (var p in room.Players)
                if (string.Equals(p.Name, trimmed, global::System.StringComparison.OrdinalIgnoreCase))
                    return p;
            foreach (var p in room.Players)
                if (!string.IsNullOrEmpty(p.Name) && p.Name.IndexOf(trimmed, global::System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return p;
            return null;
        }

        public static int ResolveCharacter(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return 0;
            string trimmed = arg.Trim();

            if (int.TryParse(trimmed, out int dataId))
            {
                if (Managers.Data.CharacterDic.ContainsKey(dataId))
                    return dataId;
                return 0;
            }

            foreach (var c in Managers.Data.CharacterDic.Values)
            {
                string typeName = c.Type.ToString();
                if (string.Equals(typeName, trimmed, global::System.StringComparison.OrdinalIgnoreCase))
                    return c.DataId;
                if (!string.IsNullOrEmpty(c.Name)
                    && (string.Equals(c.Name, trimmed, global::System.StringComparison.OrdinalIgnoreCase)
                        || c.Name.IndexOf(trimmed, global::System.StringComparison.OrdinalIgnoreCase) >= 0))
                    return c.DataId;
            }
            foreach (var c in Managers.Data.CharacterDic.Values)
                if (c.Type.ToString().IndexOf(trimmed, global::System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return c.DataId;
            return 0;
        }
    }

    [HarmonyPatch(typeof(Server.Game.HostPacketHandler), "Handle_C_CHAT_MESSAGE")]
    internal static class ForceRinPickChatPatch
    {
        private static bool Prefix(IPacketSink session, Packet packet)
        {
            try
            {
                if (!Engine.Enabled<ForceRinPickFeature>())
                    return true;

                if (!(packet?.Pkt is C_CHAT_MESSAGE msg))
                    return true;
                if (string.IsNullOrWhiteSpace(msg.Text))
                    return true;

                string prefix = ForceRinPickFeature.CommandPrefix?.Trim() ?? "!force";
                if (string.IsNullOrEmpty(prefix) || !msg.Text.TrimStart().StartsWith(prefix + " ", global::System.StringComparison.OrdinalIgnoreCase))
                    return true;

                string[] parts = msg.Text.TrimStart().Substring(prefix.Length).Trim()
                    .Split(new[] { ' ' }, 2, global::System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return false;

                string targetArg = parts[0].Trim();
                string charArg = parts.Length > 1 ? parts[1].Trim() : ForceRinPickFeature.DefaultCharacter;

                var target = ForceRinPickLogic.ResolveTarget(targetArg);
                if (target == null)
                {
                    Log.Info<ForceRinPickFeature>($"强制选角指令未命中目标：{targetArg}");
                    return false;
                }

                int charDataId = ForceRinPickLogic.ResolveCharacter(charArg);
                if (charDataId <= 0)
                {
                    Log.Info<ForceRinPickFeature>($"强制选角指令未命中角色：{charArg}");
                    return false;
                }

                ForceRinPickLogic.Mark(target.PublicInfo.PlayerId, charDataId);
                Log.Info<ForceRinPickFeature>($"强制选角 {charArg}：玩家 {target.PublicInfo.PlayerId}（{target.Name}）");
                return false;
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<ForceRinPickFeature>(ex, "强制选角指令解析失败（放行原逻辑）");
                return true;
            }
        }
    }
}
