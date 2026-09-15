using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using DT_Tools.Console.Commands.Weapon;
using Protocol;
using UnityEngine;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /beacon [x y | spawn &lt;i&gt; | lobby | error]
    ///
    /// 战略信标：掉皮掉肉不掉队！瞬移到指定坐标（跟随控制台 / 客户端）。
    ///
    /// 实现原理（按《分析MOD客户端功能数据包》第 8 节"传送"伪造路径）：
    ///   服务端 GameRoom.HandleMove → Player.Move(pos) 全程无距离校验：
    ///   仅拒绝 Hide/Sit/MoveLock（MoveLock=force respawn 后 100ms 自动复位），
    ///   位置非法时 fallback 到 MapData.ErrorPos（force respawn）。
    ///   见 0.1.14b/Server.Game/GameRoom.cs:2297-2343、Player.cs:731-769。
    ///   但客户端 PlayerManager.HandleMove 显式忽略本机 MyPlayer（仅同步他人）：
    ///     if (...MyPlayer.PublicInfo.PlayerId != pkt.PlayerId...) value.TargetPos = pkt.Pos;
    ///   见 0.1.14b/PlayerManager.cs:416-426。故 S_MOVE 不会回拉本机坐标，
    ///   需本地主动落位（与官方 S_RESPAWN 处理一致：transform.position + TargetPos）。
    ///
    /// 落位流程：
    ///   1. 本地 transform.position / TargetPos 立即设到目标点（官方 HandleRespawn 同款）；
    ///   2. 发 C_MOVE{Pos, LookLeft, Velocity=0, IsMove=false} 让服务端改 PublicInfo.Pos
    ///      并广播 S_MOVE 给其他客户端（他人会看到本机瞬移）；
    ///   3. FollowCamera.Move() 让相机跟拍。
    ///   本机下一次 UpdateMovePacket（0.1s）会用新 transform 再发一次 C_MOVE，状态自洽。
    ///
    /// 出生点数据来源：
    ///   - MapData.StartPosList：开局 GameStart 时 Util.Shuffle 后分配给各玩家的出生坐标
    ///     （GameRoom.cs:669-674），是地图级固定出生点（非按区域）。
    ///   - MapData.LobbyPos / MapData.ErrorPos：大厅点 / 非法坐标兜底点。
    ///   - 区域（RoomData）无固定出生坐标：RoomData 仅含 DataId/Type/AdjacentArea/PrefabName，
    ///     不含 Pos 字段；AreaManager.GetArea(pos) 按 224 格网格反查区域，而非区域存坐标。
    ///
    /// 权限 / 前置条件（跟随控制台 / 客户端）：
    ///   - 本机已进入对局，且 Managers.Network.GameServer 链路可用
    ///   - 本机非 Hide/Sit 状态（服务端 HandleMove 拒绝这两种状态发包）
    ///   - 坐标需落在地图可行走网格内，否则服务端强制改到 ErrorPos（force respawn）
    ///
    /// 示例:
    ///   /beacon              列出出生点坐标 + 帮助
    ///   /beacon 3360 2240    瞬移到 (3360, 2240)
    ///   /beacon spawn 2      瞬移到第 2 个出生点
    ///   /beacon lobby        瞬移到大厅点
    ///   /beacon error        瞬移到 ErrorPos（用于测试 / 兜底点）
    /// </summary>
    internal sealed class BeaconCommand : IConsoleCommand
    {
        public string   Name        => "beacon";
        public string[] Aliases     => new[] { "战略信标", "信标", "teleport", "tp", "瞬移" };
        public string   Usage       => "beacon [x y | spawn <i> | lobby | error]";
        public string   Description => "战略信标：瞬移到指定坐标。掉皮掉肉不掉队！（跟随控制台 / 客户端）";
        public string   Author      => "梦初雪";

        // 单格 224 单位（AreaManager.GetArea / ClampToMapBounds 的网格尺度）
        private const float GridScale = 224f;

        public void Execute(string[] args, WebConsole console)
        {
            // 1. 校验本机玩家 + 网络链路
            var my = WeaponPacketHelper.RequireLocalPlayer(console);
            if (my == null)
            {
                console.SetResult("{\"ok\":false,\"error\":\"not in game\"}");
                return;
            }

            var mapData = Managers.Data?.MapData;
            if (mapData == null)
            {
                console.Log("地图数据未加载（MapData 为空）。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"no mapdata\"}");
                return;
            }

            // 2. 无参数 → 列出出生点 + 帮助
            if (args.Length == 0)
            {
                console.Log(BuildHelpAndSpawns(mapData, my), LogLevel.Info);
                int spawnCount = mapData.StartPosList?.Count ?? 0;
                var spawns = new List<string>(spawnCount);
                if (spawnCount > 0)
                {
                    foreach (var p in mapData.StartPosList)
                    {
                        spawns.Add("{\"x\":" + p.X + ",\"y\":" + p.Y
                                 + ",\"room\":\"" + EscapeJson(ResolveRoomLabel(p, mapData).raw) + "\"}");
                    }
                }
                console.SetResult("{\"ok\":true,\"spawns\":" + spawnCount
                                 + ",\"list\":[" + string.Join(",", spawns) + "]}");
                return;
            }

            // 3. 解析目标坐标
            if (!TryResolveTarget(args, mapData, console, out var target, out string label))
            {
                console.SetResult("{\"ok\":false,\"error\":\"bad args\"}");
                return;
            }

            // 4. 状态门禁：Hide/Sit 服务端会拒收 C_MOVE
            if (my.State == EPlayerState.Hide || my.State == EPlayerState.Sit)
            {
                console.Log($"当前状态 {my.State} 无法瞬移（服务端 HandleMove 拒收 Hide/Sit）。请先脱离藏身处/座位。", LogLevel.Warning);
                console.SetResult("{\"ok\":false,\"error\":\"state locked\"}");
                return;
            }

            // 5. 越界预警（不阻断：服务端会兜底到 ErrorPos）
            WarnIfOutOfBounds(target, mapData, console);

            // 6. 瞬移
            Teleport(my, target);
            var roomLabel = ResolveRoomLabel(target, mapData);
            console.Log($"[战略信标] 已瞬移到 {label} → ({target.X:F0}, {target.Y:F0})  [{roomLabel.localized}]。掉皮掉肉不掉队！",
                LogLevel.Message);
            console.SetResult("{\"ok\":true,\"x\":" + target.X + ",\"y\":" + target.Y
                             + ",\"room\":\"" + EscapeJson(roomLabel.raw) + "\"}");
        }

        // ── 解析目标坐标 ────────────────────────────────────
        private static bool TryResolveTarget(string[] args, Data.MapData mapData, WebConsole console,
            out PosInfo target, out string label)
        {
            target = null;
            label = null;

            // beacon lobby | error
            if (args.Length == 1)
            {
                string key = args[0].ToLowerInvariant();
                if (key == "lobby" || key == "大厅")
                {
                    if (mapData.LobbyPos == null)
                    {
                        console.Log("MapData.LobbyPos 未配置。", LogLevel.Warning);
                        return false;
                    }
                    target = mapData.LobbyPos;
                    label = "大厅点 LobbyPos";
                    return true;
                }
                if (key == "error" || key == "兜底")
                {
                    if (mapData.ErrorPos == null)
                    {
                        console.Log("MapData.ErrorPos 未配置。", LogLevel.Warning);
                        return false;
                    }
                    target = mapData.ErrorPos;
                    label = "兜底点 ErrorPos";
                    return true;
                }
            }

            // beacon spawn <i>
            if (args.Length == 2
                && (args[0].Equals("spawn", StringComparison.OrdinalIgnoreCase) || args[0] == "出生"))
            {
                var list = mapData.StartPosList;
                if (list == null || list.Count == 0)
                {
                    console.Log("MapData.StartPosList 为空（不在对局地图 / 未加载）。", LogLevel.Warning);
                    return false;
                }
                if (!int.TryParse(args[1], out int idx) || idx < 1 || idx > list.Count)
                {
                    console.Log($"出生点序号无效：{args[1]}（范围 1..{list.Count}）。", LogLevel.Warning);
                    return false;
                }
                target = list[idx - 1];
                label = $"出生点 #{idx}";
                return true;
            }

            // beacon <x> <y>
            if (args.Length == 2
                && float.TryParse(args[0], out float x)
                && float.TryParse(args[1], out float y))
            {
                target = new PosInfo { X = x, Y = y };
                label = "指定坐标";
                return true;
            }

            console.Log("参数无效。用法：/beacon [x y | spawn <i> | lobby | error]", LogLevel.Warning);
            return false;
        }

        // ── 越界预警 ─────────────────────────────────────────
        // 服务端 AreaManager.ValidPosition 按 224 网格 + MapArray 判定，非法会 fallback 到 ErrorPos。
        // 这里只做粗略边界提示，不做精确可行走判定（避免依赖服务端 MapArray）。
        private static void WarnIfOutOfBounds(PosInfo pos, Data.MapData mapData, WebConsole console)
        {
            if (mapData.MapSize == null || mapData.MapOffset == null) return;
            float minX = mapData.MapOffset.X * GridScale;
            float minY = mapData.MapOffset.Y * GridScale;
            float maxX = (mapData.MapOffset.X + mapData.MapSize.X) * GridScale - 1f;
            float maxY = (mapData.MapOffset.Y + mapData.MapSize.Y) * GridScale - 1f;
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY)
            {
                console.Log($"⚠ 坐标 ({pos.X:F0}, {pos.Y:F0}) 超出地图边界 [{minX:F0}~{maxX:F0}, {minY:F0}~{maxY:F0}]，"
                          + "服务端将强制改到 ErrorPos。", LogLevel.Warning);
            }
        }

        // ── 瞬移（与官方 HandleRespawn 同款：transform + TargetPos + 相机） ───
        private static void Teleport(MyPlayer my, PosInfo pos)
        {
            // 1. 本地立即落位
            my.TargetPos = pos;
            my.transform.position = new Vector3(pos.X, pos.Y, 0f);

            // 2. 发包同步：服务端 HandleMove → player.Move(pos) 广播 S_MOVE 给其他客户端
            //    Velocity=0 / IsMove=false 表示瞬移而非行走
            Managers.Network.GameServer.Send(new C_MOVE
            {
                Pos = pos,
                LookLeft = my.PublicInfo.LookLeft,
                Velocity = 0f,
                IsMove = false
            });

            // 3. 相机跟拍
            try
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var fc = cam.GetComponent<FollowCamera>();
                    fc?.Move();
                }
            }
            catch (Exception ex)
            {
                // 相机失败不影响瞬移本身
                Debug.LogWarning($"[战略信标] FollowCamera.Move 异常: {ex.Message}");
            }
        }

        // ── 帮助 + 出生点列表 ───────────────────────────────
        private static string BuildHelpAndSpawns(Data.MapData mapData, MyPlayer my)
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 战略信标 · 掉皮掉肉不掉队 ━━━");
            sb.AppendLine("瞬移到指定坐标。直接发包 C_MOVE，服务端无距离校验。");
            sb.AppendLine();

            // 当前坐标
            sb.AppendLine("【当前位置】");
            var curRoom = ResolveRoomLabel(my.PublicInfo.Pos, mapData);
            sb.AppendLine($"  ({my.PublicInfo.Pos.X:F0}, {my.PublicInfo.Pos.Y:F0})  状态={my.State}  区域=[{curRoom.localized}]");
            sb.AppendLine();

            // 出生点
            sb.AppendLine("【默认出生点 StartPosList】");
            var list = mapData.StartPosList;
            if (list == null || list.Count == 0)
            {
                sb.AppendLine("  （无）— 不在对局地图 / 未加载（大厅阶段为空属正常）");
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var r = ResolveRoomLabel(list[i], mapData);
                    sb.AppendLine($"  #{i + 1,-2} ({list[i].X:F0}, {list[i].Y:F0})  →  {r.localized}");
                }
            }
            sb.AppendLine();

            // 大厅 / 兜底点
            sb.AppendLine("【其它坐标】");
            sb.AppendLine($"  lobby  (大厅点)  ({FormatPos(mapData.LobbyPos)})  →  {ResolveRoomLabel(mapData.LobbyPos, mapData).localized}");
            sb.AppendLine($"  error  (兜底点)  ({FormatPos(mapData.ErrorPos)})  →  {ResolveRoomLabel(mapData.ErrorPos, mapData).localized}");
            sb.AppendLine();

            // 区域说明
            sb.AppendLine("【区域出生点】");
            sb.AppendLine("  区域（RoomData）无固定出生坐标：仅含 DataId/Type/AdjacentArea/PrefabName，");
            sb.AppendLine("  不含 Pos 字段；出生点统一存于 MapData.StartPosList（地图级，非区域级）。");
            sb.AppendLine("  房间名按 224 网格反查 MapArray → ERoomType → TextDic 本地化名得到。");
            sb.AppendLine();

            // 用法
            sb.AppendLine("【用法】");
            sb.AppendLine("  /beacon              列出出生点坐标 + 帮助");
            sb.AppendLine("  /beacon <x> <y>      瞬移到指定坐标");
            sb.AppendLine("  /beacon spawn <i>    瞬移到第 <i> 个出生点（1 起）");
            sb.AppendLine("  /beacon lobby        瞬移到大厅点");
            sb.AppendLine("  /beacon error        瞬移到兜底点 ErrorPos");
            sb.AppendLine();
            sb.AppendLine("【注意】");
            sb.AppendLine("  - Hide/Sit 状态服务端拒收 C_MOVE，请先脱离藏身处/座位");
            sb.AppendLine("  - 坐标超出可行走网格时，服务端会强制改到 ErrorPos");
            sb.AppendLine("  - 跟随控制台 / 客户端：非房主可用，仅作用于本机");
            return sb.ToString();
        }

        // ── 坐标 → 房间名反查 ───────────────────────────────
        // 与服务端 AreaManager.GetArea 同款算法：
        //   gridX = (int)(pos.X / 224) - (int)MapOffset.X
        //   gridY = (int)(pos.Y / 224) - (int)MapOffset.Y
        //   ERoomType = (ERoomType)MapArray[gridX][gridY]
        // 再用 roomType.ToString() 查 TextDic 拿本地化名（与 WeaponPacketHelper.GetRoomLabel 同源）。
        // 见 0.1.14b/Server.Game/AreaManager.cs:74-80、DataManager.cs:28(MapArray)、30(TextDic)。
        private static (string localized, string raw) ResolveRoomLabel(PosInfo pos, Data.MapData mapData)
        {
            if (pos == null) return ("未知", "Unknown");
            if (mapData?.MapOffset == null) return ("未知", "Unknown");

            var mapArray = Managers.Data?.MapArray;
            if (mapArray == null || mapArray.Count == 0) return ("未知", "Unknown");

            int gx = (int)(pos.X / GridScale) - (int)mapData.MapOffset.X;
            int gy = (int)(pos.Y / GridScale) - (int)mapData.MapOffset.Y;
            if (gx < 0 || gy < 0 || gx >= mapArray.Count || gy >= mapArray[0].Count)
                return ("地图外", "OutOfBounds");

            byte b = mapArray[gx][gy];
            if (b == 0) return ("不可通行", "Blocked");

            var roomType = (ERoomType)b;
            string key = roomType.ToString();
            var textDic = Managers.Data?.TextDic;
            string localized = (textDic != null && textDic.TryGetValue(key, out var text) && text != null)
                ? text.Text
                : key;
            return (localized, key);
        }

        private static string FormatPos(PosInfo p)
        {
            return p == null ? "未配置" : $"{p.X:F0}, {p.Y:F0}";
        }

        // 最小 JSON 字符串转义（避免 room 名含 " 或 \ 时破坏 JSON）
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 4);
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
