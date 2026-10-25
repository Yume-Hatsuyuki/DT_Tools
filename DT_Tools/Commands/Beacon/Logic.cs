using Data;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Commands.Beacon
{
    internal static class BeaconLogic
    {
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
                    var other = LocalPlayer.FindClientPlayer(pid);
                    if (other == null)
                    {
                        error = $"玩家表中找不到存活的 #{pid}（未入局 / 已死亡被 S_DESPAWN 移除 / 旁观者）。"
                              + "可用无参 /beacon 查看在线玩家列表。";
                        return false;
                    }
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
