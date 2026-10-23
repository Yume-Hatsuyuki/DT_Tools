using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace DT_Tools.Commands.Modded
{
    /// <summary>
    /// /modded 业务：按房主侧权威数据收集玩家 modded 标记。
    /// </summary>
    /// <remarks>
    /// Server.Game.Player.IsModded 是 0.1.21 新增属性，编译目标 0.1.20a 的 Assembly-CSharp
    /// 中不存在 → 用 AccessTools.Property 反射读取，旧版本返回 null（=版本不支持），
    /// 0.1.21+ 返回真实 bool。
    /// </remarks>
    internal static class ModdedLogic
    {
        /// <summary>
        /// Server.Game.Player.IsModded 属性（0.1.21+）；编译目标 0.1.20a 无此属性，反射为 null。
        /// </summary>
        private static readonly PropertyInfo IsModdedProp =
            AccessTools.Property(typeof(global::Server.Game.Player), "IsModded");

        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public bool IsHost;
            public bool IsSelf;
            /// <summary>null=版本不支持（0.1.20a 无 IsModded）；true/false=0.1.21+ 真实标记。</summary>
            public bool? Modded;
        }

        public static List<Entry> Collect(global::Server.Game.GameRoom room, int filterPid)
        {
            var list = new List<Entry>();
            int hostId = room.Host?.PublicInfo?.PlayerId ?? 0;
            int myId = Managers.Player?.MyPlayerID ?? 0;
            bool supported = IsModdedProp != null;

            foreach (var p in room.Players.OrderBy(x => x?.PublicInfo?.PlayerId ?? 0))
            {
                if (p?.PublicInfo == null)
                    continue;
                int pid = p.PublicInfo.PlayerId;
                if (filterPid > 0 && pid != filterPid)
                    continue;

                bool? modded = null;
                if (supported)
                {
                    try
                    {
                        modded = (bool?)IsModdedProp.GetValue(p);
                    }
                    catch
                    {
                        modded = null;
                    }
                }

                list.Add(new Entry
                {
                    Pid = pid,
                    Name = p.Name ?? "(未命名)",
                    IsHost = pid == hostId,
                    IsSelf = pid == myId,
                    Modded = modded,
                });
            }
            return list;
        }
    }
}
