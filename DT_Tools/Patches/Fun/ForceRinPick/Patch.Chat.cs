using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.ForceRinPick
{
    /// <summary>强制选角标记与目标/角色解析（服务端进程内共享状态）。</summary>
    internal static class ForceRinPickLogic
    {
        /// <summary>被强制玩家 PlayerId → 目标角色 DataId 映射（一次强制：命中后即移除）。</summary>
        private static readonly Dictionary<int, int> _forced = new Dictionary<int, int>();

        /// <summary>标记目标玩家：下次选角强制为指定角色（角色不存在则忽略）。</summary>
        public static void Mark(int playerId, int characterDataId)
        {
            if (characterDataId <= 0) return;
            _forced[playerId] = characterDataId;
        }

        /// <summary>选角时消费标记：返回被强制选的角色 DataId（0 表示无标记）。</summary>
        public static int Consume(int playerId)
        {
            if (_forced.TryGetValue(playerId, out int dataId))
            {
                _forced.Remove(playerId);
                return dataId;
            }
            return 0;
        }

        /// <summary>按指令参数（玩家名或 PlayerId）在房间内解析目标玩家。</summary>
        public static Server.Game.Player ResolveTarget(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return null;
            var room = Server.Game.GameRoom.Instance;
            if (room == null) return null;

            string trimmed = arg.Trim();
            // 纯数字 → PlayerId 精确匹配
            if (int.TryParse(trimmed, out int id))
            {
                foreach (var p in room.Players)
                    if (p.PublicInfo.PlayerId == id)
                        return p;
                return null;
            }
            // 玩家名：先精确（忽略大小写），再包含匹配（方便带空格/后缀）
            foreach (var p in room.Players)
                if (string.Equals(p.Name, trimmed, global::System.StringComparison.OrdinalIgnoreCase))
                    return p;
            foreach (var p in room.Players)
                if (!string.IsNullOrEmpty(p.Name) && p.Name.IndexOf(trimmed, global::System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return p;
            return null;
        }

        /// <summary>按角色参数（枚举名/角色名/DataId）解析角色 DataId。</summary>
        public static int ResolveCharacter(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return 0;
            string trimmed = arg.Trim();

            // 纯数字 → DataId 直接匹配
            if (int.TryParse(trimmed, out int dataId))
            {
                if (Managers.Data.CharacterDic.ContainsKey(dataId))
                    return dataId;
                return 0;
            }

            foreach (var c in Managers.Data.CharacterDic.Values)
            {
                // 枚举名（Rin/Madeline/...）与角色名（Name 字段）精确匹配
                string typeName = c.Type.ToString();
                if (string.Equals(typeName, trimmed, global::System.StringComparison.OrdinalIgnoreCase))
                    return c.DataId;
                if (!string.IsNullOrEmpty(c.Name)
                    && (string.Equals(c.Name, trimmed, global::System.StringComparison.OrdinalIgnoreCase)
                        || c.Name.IndexOf(trimmed, global::System.StringComparison.OrdinalIgnoreCase) >= 0))
                    return c.DataId;
            }
            // 枚举名包含匹配（大小写不敏感）
            foreach (var c in Managers.Data.CharacterDic.Values)
                if (c.Type.ToString().IndexOf(trimmed, global::System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return c.DataId;
            return 0;
        }
    }

    /// <summary>
    /// 服务端聊天入口（0.1.16b HostPacketHandler.Handle_C_CHAT_MESSAGE，static）：解析
    /// `!force 目标 [角色]` 指令——命中则标记目标玩家并拦截本次聊天（指令文本不广播全房）。
    /// 仅在房主进程执行（客户端上 HostPacketHandler 链路不运行），无需 IsHost 判定。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.HostPacketHandler), "Handle_C_CHAT_MESSAGE")]
    internal static class ForceRinPickChatPatch
    {
        private static bool Prefix(IPacketSink session, Packet packet)
        {
            try
            {
                if (!Engine.Enabled<ForceRinPickFeature>())
                    return true; // 未开启：放行原逻辑

                if (!(packet?.Pkt is C_CHAT_MESSAGE msg))
                    return true;
                if (string.IsNullOrWhiteSpace(msg.Text))
                    return true;

                string prefix = ForceRinPickFeature.CommandPrefix?.Trim() ?? "!force";
                if (string.IsNullOrEmpty(prefix) || !msg.Text.TrimStart().StartsWith(prefix + " ", global::System.StringComparison.OrdinalIgnoreCase))
                    return true; // 非指令：放行

                string[] parts = msg.Text.TrimStart().Substring(prefix.Length).Trim()
                    .Split(new[] { ' ' }, 2, global::System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return false; // 空指令：拦截不广播

                string targetArg = parts[0].Trim();
                string charArg = parts.Length > 1 ? parts[1].Trim() : ForceRinPickFeature.DefaultCharacter;

                var target = ForceRinPickLogic.ResolveTarget(targetArg);
                if (target == null)
                {
                    Log.Info<ForceRinPickFeature>($"强制选角指令未命中目标：{targetArg}");
                    return false; // 指令已拦截，不广播（未命中也静默，避免刷屏）
                }

                int charDataId = ForceRinPickLogic.ResolveCharacter(charArg);
                if (charDataId <= 0)
                {
                    Log.Info<ForceRinPickFeature>($"强制选角指令未命中角色：{charArg}");
                    return false;
                }

                ForceRinPickLogic.Mark(target.PublicInfo.PlayerId, charDataId);
                Log.Info<ForceRinPickFeature>($"强制选角 {charArg}：玩家 {target.PublicInfo.PlayerId}（{target.Name}）");
                return false; // 拦截：指令文本不广播全房
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<ForceRinPickFeature>("强制选角指令解析失败（放行原逻辑）：" + ex.Message);
                return true;
            }
        }
    }
}
