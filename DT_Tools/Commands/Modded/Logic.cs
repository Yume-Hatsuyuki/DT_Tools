using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Commands.Modded
{
    internal static class ModdedLogic
    {
        private static readonly PropertyInfo IsModdedProp =
            AccessTools.Property(typeof(global::Server.Game.Player), "IsModded");

        private static readonly Type SnapshotMirrorType = AccessTools.TypeByName("Server.Game.SnapshotMirror");

        private static readonly Type SnapshotCodecType = AccessTools.TypeByName("Server.Game.SnapshotCodec");

        private const string LogTag = "Modded";

        public enum CollectSource
        {
            Host,
            Snapshot,
        }

        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public bool IsHost;
            public bool IsSelf;
            public bool? Modded;
        }

        public sealed class CollectResult
        {
            public CollectSource Source;
            public List<Entry> Players = new List<Entry>();
            public string ErrorCode;
            public string ErrorMessage;

            public bool Ok => ErrorCode == null;
        }

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
