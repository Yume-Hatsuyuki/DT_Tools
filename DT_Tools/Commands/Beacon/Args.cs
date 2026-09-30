using System;
using DT_Tools.Game;

namespace DT_Tools.Commands.Beacon
{
    /// <summary>/beacon 参数形状：坐标 / 出生点序号 / 玩家号 / lobby / error（纯解析，不查游戏数据）。</summary>
    internal sealed class BeaconArgs
    {
        public enum Mode
        {
            Coord,
            Spawn,
            Player,
            Lobby,
            Error
        }

        public Mode Kind { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public int SpawnIndex { get; private set; }   // 1 起
        public int PlayerId { get; private set; }

        public static bool TryParse(string[] args, out BeaconArgs parsed, out string error)
        {
            parsed = new BeaconArgs();
            error = null;

            // beacon lobby | error（含中文别名）
            if (args.Length == 1)
            {
                string key = args[0].ToLowerInvariant();
                if (key == "lobby" || key == "大厅")
                {
                    parsed.Kind = Mode.Lobby;
                    return true;
                }
                if (key == "error" || key == "兜底")
                {
                    parsed.Kind = Mode.Error;
                    return true;
                }
            }

            // beacon spawn <#i>
            if (args.Length == 2
                && (args[0].Equals("spawn", StringComparison.OrdinalIgnoreCase) || args[0] == "出生"))
            {
                if (!TargetSpec.TryParseId(args[1], out int idx))
                {
                    error = $"无效的序号: {args[1]}（出生点写法 /beacon spawn #<i>，# 可省略）。";
                    return false;
                }
                parsed.Kind = Mode.Spawn;
                parsed.SpawnIndex = idx;
                return true;
            }

            // beacon player <#id>
            if (args.Length == 2
                && (args[0].Equals("player", StringComparison.OrdinalIgnoreCase) || args[0] == "玩家"))
            {
                if (!TargetSpec.TryParseId(args[1], out int pid))
                {
                    error = $"无效的 ID: {args[1]}（应为 #<数字> 或纯数字）";
                    return false;
                }
                parsed.Kind = Mode.Player;
                parsed.PlayerId = pid;
                return true;
            }

            // beacon <x> <y>
            if (args.Length == 2
                && float.TryParse(args[0], out float x)
                && float.TryParse(args[1], out float y))
            {
                parsed.Kind = Mode.Coord;
                parsed.X = x;
                parsed.Y = y;
                return true;
            }

            error = "参数无效。用法：/beacon [x y | spawn <#i> | player <#id> | lobby | error]";
            return false;
        }
    }
}
