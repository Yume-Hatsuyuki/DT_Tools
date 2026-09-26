using Data;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.Beacon
{
    /// <summary>/beacon 业务：目标落点解析（查游戏数据）+ 越界预警。</summary>
    internal static class BeaconLogic
    {
        /// <summary>
        /// 把解析好的参数解析为具体落点（拷贝 PosInfo，避免与他人 PosInfo 别名共享）。
        /// targetPlayerId &gt; 0 表示目标来自 player #id（仅用于结果 JSON 回带）。
        /// </summary>
        public static bool ResolveTarget(BeaconArgs args, MapData mapData, MyPlayer my,
            out PosInfo pos, out string label, out int targetPlayerId, out string error)
        {
            pos = null;
            label = null;
            targetPlayerId = 0;

            switch (args.Kind)
            {
                case BeaconArgs.Mode.Lobby:
                    if (mapData.LobbyPos == null)
                    {
                        error = "MapData.LobbyPos 未配置。";
                        return false;
                    }
                    pos = mapData.LobbyPos;
                    label = "大厅点 LobbyPos";
                    break;

                case BeaconArgs.Mode.Error:
                    if (mapData.ErrorPos == null)
                    {
                        error = "MapData.ErrorPos 未配置。";
                        return false;
                    }
                    pos = mapData.ErrorPos;
                    label = "兜底点 ErrorPos";
                    break;

                case BeaconArgs.Mode.Spawn:
                {
                    var list = mapData.StartPosList;
                    if (list == null || list.Count == 0)
                    {
                        error = "MapData.StartPosList 为空（不在对局地图 / 未加载）。";
                        return false;
                    }
                    if (args.SpawnIndex < 1 || args.SpawnIndex > list.Count)
                    {
                        error = $"出生点序号超出范围：#{args.SpawnIndex}（有效范围 1..{list.Count}）。";
                        return false;
                    }
                    pos = list[args.SpawnIndex - 1];
                    label = $"出生点 #{args.SpawnIndex}";
                    break;
                }

                case BeaconArgs.Mode.Player:
                {
                    int pid = args.PlayerId;
                    if (pid == my.PublicInfo.PlayerId)
                    {
                        error = $"#{pid} 就是本机自己，当前位置即目标，无需瞬移。";
                        return false;
                    }
                    // 0.1.15b PlayerManager.cs:42 Players——只含存活且已 Spawn 的玩家；
                    // 死亡玩家收到 S_DESPAWN 后被移出该表，旁观者不入表，本机 MyPlayer 不在表内。
                    var other = LocalPlayer.FindClientPlayer(pid);
                    if (other == null)
                    {
                        error = $"玩家表中找不到存活的 #{pid}（未入局 / 已死亡被 S_DESPAWN 移除 / 旁观者）。"
                              + "可用无参 /beacon 查看在线玩家列表。";
                        return false;
                    }
                    // TargetPos 是 S_MOVE 最新落点（服务端当前 PublicInfo.Pos）
                    var src = other.TargetPos ?? other.PublicInfo.Pos;
                    pos = new PosInfo { X = src.X, Y = src.Y };
                    label = $"玩家 {other.Name}（#{pid}）";
                    targetPlayerId = pid;
                    break;
                }

                default:
                    pos = new PosInfo { X = args.X, Y = args.Y };
                    label = "指定坐标";
                    break;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 越界预警（不阻断）：服务端 HandleMove 对存活玩家按 AreaManager.ValidPosition
        /// （224 网格 + MapArray，0.1.15b Server.Game/AreaManager.cs:82）判定，越界时
        /// <b>仅 Survive / Detective 阶段</b> fallback 到 ErrorPos（0.1.15b
        /// Server.Game/GameRoom.cs:2349-2354）；其余阶段（Lobby/Vote/Trial 等）整包
        /// 静默丢弃——本机已本地落位，但服务端不广播 S_MOVE，他人视角仍在原地。
        /// 这里只做粗略边界提示，不做精确可行走判定（避免依赖服务端 MapArray）。
        /// </summary>
        public static string OutOfBoundsWarning(PosInfo pos, MapData mapData)
        {
            if (pos == null || mapData?.MapSize == null || mapData.MapOffset == null)
                return null;

            float minX = mapData.MapOffset.X * RoomLabel.GridScale;
            float minY = mapData.MapOffset.Y * RoomLabel.GridScale;
            float maxX = (mapData.MapOffset.X + mapData.MapSize.X) * RoomLabel.GridScale - 1f;
            float maxY = (mapData.MapOffset.Y + mapData.MapSize.Y) * RoomLabel.GridScale - 1f;
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY)
            {
                return $"⚠ 坐标 ({pos.X:F0}, {pos.Y:F0}) 超出地图边界 [{minX:F0}~{maxX:F0}, {minY:F0}~{maxY:F0}]，"
                     + "服务端仅会在生存/调查阶段将其强制改到 ErrorPos，其余阶段直接丢弃本次移动。";
            }
            return null;
        }
    }
}
