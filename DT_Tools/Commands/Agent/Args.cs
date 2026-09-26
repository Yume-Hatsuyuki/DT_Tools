using System;
using System.Collections.Generic;

namespace DT_Tools.Commands.Agent
{
    /// <summary>/agent 参数解析：all / 任务ID / 任务别名 / #设备号 / stop（对应旧 AgentCommand 内联解析）。</summary>
    internal sealed class AgentArgs
    {
        /// <summary>任务名/中文别名 → ESchoolMission 底层值（ScFishing=40、ScMorseCode=37 等，0.1.15b Protocol/ESchoolMission.cs）。</summary>
        private static readonly Dictionary<string, int> AliasMap =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "morse", 37 }, { "摩斯", 37 }, { "摩斯密码", 37 },
                { "surgery", 1 }, { "手术", 1 },
                { "spray", 2 }, { "喷雾", 2 }, { "合成喷雾", 2 },
                { "miner", 3 }, { "矿工", 3 },
                { "candle", 4 }, { "蜡烛", 4 },
                { "collector", 6 }, { "收集", 6 },
                { "mineralcraft", 7 }, { "放矿", 7 },
                { "craft", 8 }, { "合成", 8 },
                { "essence", 9 }, { "分离", 9 },
                { "microscope", 10 }, { "显微镜", 10 },
                { "bookrune", 11 }, { "符文书", 11 },
                { "rune", 12 }, { "符文", 12 },
                { "warp", 13 }, { "传送", 13 },
                { "boiler", 14 }, { "制冰", 14 }, { "锅炉", 14 },
                { "drink", 15 }, { "调酒", 15 },
                { "shaker", 16 }, { "摇酒", 16 },
                { "fixpc", 17 }, { "修电脑", 17 }, { "电脑", 17 },
                { "manikin", 18 }, { "人体模型", 18 }, { "假人", 18 },
                { "spraycancer", 20 }, { "杀菌", 20 },
                { "potionalchemist", 21 }, { "炼金药", 21 },
                { "shakerdrink", 22 }, { "献酒", 22 },
                { "charge", 23 }, { "充电", 23 },
                { "battery", 24 }, { "满电", 24 },
                { "batteryminer", 25 }, { "矿工电池", 25 },
                { "batterywarp", 26 }, { "传送电池", 26 },
                { "batterybio", 27 }, { "生物电池", 27 },
                { "mushroom", 28 }, { "蘑菇", 28 },
                { "mushroomalchemist", 30 }, { "蘑菇炼金", 30 },
                { "nintendo", 34 }, { "任天堂", 34 },
                { "potion", 35 }, { "药剂", 35 },
                { "fire", 36 }, { "火焰", 36 },
                { "fish", 40 }, { "钓鱼", 40 },
            };

        public bool IsStop { get; private set; }
        public bool IsAll { get; private set; }
        public AgentFilter Filter { get; private set; }

        /// <summary>
        /// 解析首个参数：null/空 = 无过滤（预览/全清），stop = 停止，
        /// all/全部/全清 = 全清，其余按 任务ID/别名/#设备号 解析。
        /// </summary>
        public static bool TryParse(string[] args, out AgentArgs parsed, out string error)
        {
            parsed = new AgentArgs();
            error = null;

            if (args.Length > 0 && (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase)
                                    || args[0] == "停止"))
            {
                parsed.IsStop = true;
                return true;
            }

            if (args.Length == 0)
                return true; // 无参：预览模式

            if (string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase)
                || args[0] == "全部" || args[0] == "全清")
            {
                parsed.IsAll = true;
                return true;
            }

            if (!TryParseFilter(args[0], out var filter))
            {
                error = $"无法识别: {args[0]}\n  /agent 1 | /agent 手术 | /agent #10084";
                return false;
            }
            parsed.Filter = filter;
            return true;
        }

        private static bool TryParseFilter(string s, out AgentFilter filter)
        {
            filter = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s[0] == '#')
            {
                if (int.TryParse(s.Substring(1), out int did) && did > 0)
                {
                    filter = AgentFilter.ByDevice(did);
                    return true;
                }
                return false;
            }
            if (AliasMap.TryGetValue(s, out int aid))
            {
                filter = AgentFilter.ByMission(aid);
                return true;
            }
            if (int.TryParse(s, out int mid) && mid > 0)
            {
                filter = AgentFilter.ByMission(mid);
                return true;
            }
            return false;
        }
    }
}
