using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Commands.Modded
{
    /// <summary>
    /// /modded 业务：收集各玩家的 modded 标记。房主读本机房间，客户端读房主广播的快照。
    /// </summary>
    /// <remarks>
    /// 两条数据源的字段都是 0.1.21 才有的，编译目标的旧版本中可能不存在，
    /// 所以类型与属性全部用反射访问。缺失时返回 null（=版本不支持），不抛异常。
    /// </remarks>
    internal static class ModdedLogic
    {
        /// <summary>服务端玩家的 IsModded（0.1.21c Server.Game/Player.cs:110）。</summary>
        private static readonly PropertyInfo IsModdedProp =
            AccessTools.Property(typeof(global::Server.Game.Player), "IsModded");

        /// <summary>快照缓存入口（0.1.21c Server.Game/SnapshotMirror.cs:5）。</summary>
        private static readonly Type SnapshotMirrorType = AccessTools.TypeByName("Server.Game.SnapshotMirror");

        /// <summary>快照解析入口（0.1.21c Server.Game/SnapshotCodec.cs:113 Parse）。</summary>
        private static readonly Type SnapshotCodecType = AccessTools.TypeByName("Server.Game.SnapshotCodec");

        private const string LogTag = "Modded";

        public enum CollectSource
        {
            /// <summary>房主本机的房间数据。</summary>
            Host,
            /// <summary>房主广播的最近一份房间快照。</summary>
            Snapshot,
        }

        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public bool IsHost;
            public bool IsSelf;
            /// <summary>null=版本不支持（该版本没有 IsModded 字段）；true/false=真实标记。</summary>
            public bool? Modded;
        }

        public sealed class CollectResult
        {
            public CollectSource Source;
            public List<Entry> Players = new List<Entry>();
            /// <summary>失败时的短错误码（小写英文）；成功为 null。</summary>
            public string ErrorCode;
            /// <summary>失败时给人看的中文原因。</summary>
            public string ErrorMessage;

            public bool Ok => ErrorCode == null;
        }

        /// <summary>
        /// 房主走 GameRoom 本机数据；客户端走快照。快照每秒由房主广播一次，客户端只能读到它最近收到的那份。
        /// </summary>
        /// <param name="filterPid">只取该玩家 ID；0 表示全部。</param>
        /// <returns>成功时 Ok 为 true，Players 为按 ID 升序的结果；失败时 ErrorCode 与 ErrorMessage 有值。</returns>
        public static CollectResult Collect(int filterPid)
        {
            if (HostGuard.IsHost)
            {
                var room = global::Server.Game.GameRoom.Instance;
                if (room == null)
                    return Fail(CollectSource.Host, "no room", "当前没有活动的游戏房间。");
                return new CollectResult
                {
                    Source = CollectSource.Host,
                    Players = FromRoom(room, filterPid),
                };
            }
            return FromSnapshot(filterPid);
        }

        private static List<Entry> FromRoom(global::Server.Game.GameRoom room, int filterPid)
        {
            var list = new List<Entry>();
            int hostId = room.Host?.PublicInfo?.PlayerId ?? 0;
            int myId = Managers.Player?.MyPlayerID ?? 0;

            foreach (var p in room.Players.OrderBy(x => x?.PublicInfo?.PlayerId ?? 0))
            {
                if (p?.PublicInfo == null)
                    continue;
                int pid = p.PublicInfo.PlayerId;
                if (filterPid > 0 && pid != filterPid)
                    continue;

                list.Add(new Entry
                {
                    Pid = pid,
                    Name = p.Name ?? "(未命名)",
                    IsHost = pid == hostId,
                    IsSelf = pid == myId,
                    Modded = ReadServerModded(p),
                });
            }
            return list;
        }

        private static bool? ReadServerModded(global::Server.Game.Player p)
        {
            if (IsModdedProp == null)
                return null;
            try
            {
                return (bool?)IsModdedProp.GetValue(p);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从最近一份快照读取全员标记。快照里的 IsModded 与房主 Player.IsModded 来自同一份自报
        /// （0.1.21c Server.Game/SnapshotCodec.cs:74 直接写入），两条路径的标记含义一致。
        /// </summary>
        /// <param name="filterPid">只取该玩家 ID；0 表示全部。</param>
        /// <returns>读取成功返回玩家列表；快照缺失、版本不支持或解析失败时返回带错误码的结果。</returns>
        private static CollectResult FromSnapshot(int filterPid)
        {
            if (SnapshotMirrorType == null || SnapshotCodecType == null)
                return Fail(CollectSource.Snapshot, "no snapshot", "当前游戏版本不提供房间快照，无法读取 modded 标记。");

            try
            {
                bool hasSnapshot = (bool)AccessTools.Property(SnapshotMirrorType, "HasSnapshot").GetValue(null);
                if (!hasSnapshot)
                    return Fail(CollectSource.Snapshot, "no snapshot", "尚未收到房主广播的房间快照（需要先进入房间，快照约每秒刷新一次）。");

                byte[] payload = (byte[])AccessTools.Property(SnapshotMirrorType, "LatestPayload").GetValue(null);
                object snapshot = AccessTools.Method(SnapshotCodecType, "Parse").Invoke(null, new object[] { payload });
                var players = (IEnumerable)Read(snapshot, "Players");

                int myId = Managers.Player?.MyPlayerID ?? 0;
                var list = new List<Entry>();
                foreach (object sp in players)
                {
                    int pid = (int)Read(sp, "PlayerId");
                    if (filterPid > 0 && pid != filterPid)
                        continue;

                    list.Add(new Entry
                    {
                        Pid = pid,
                        Name = (string)Read(sp, "Name") ?? "(未命名)",
                        IsHost = (bool?)Read(sp, "IsHost") ?? false,
                        IsSelf = pid == myId,
                        Modded = (bool?)Read(sp, "IsModded"),
                    });
                }

                return new CollectResult
                {
                    Source = CollectSource.Snapshot,
                    Players = list.OrderBy(e => e.Pid).ToList(),
                };
            }
            catch (Exception ex)
            {
                Log.Warn(LogTag, ex, "读取房间快照失败");
                return Fail(CollectSource.Snapshot, "snapshot read failed", "读取房间快照失败，详见 BepInEx 日志。");
            }
        }

        /// <summary>
        /// 读取实例属性，用于兼容字段缺失的旧版本。
        /// </summary>
        /// <param name="target">要读取的对象，不能为 null。</param>
        /// <param name="name">属性名。</param>
        /// <returns>属性值；属性不存在时返回 null。</returns>
        private static object Read(object target, string name)
        {
            return AccessTools.Property(target.GetType(), name)?.GetValue(target);
        }

        private static CollectResult Fail(CollectSource source, string code, string message) => new CollectResult
        {
            Source = source,
            ErrorCode = code,
            ErrorMessage = message,
        };
    }
}
