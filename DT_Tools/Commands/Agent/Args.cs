using System;
using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Commands.Agent
{
    /// <summary>/agent 参数解析：all / 任务ID / 任务别名 / #设备号 / stop（对应旧 AgentCommand 内联解析）。</summary>
    internal sealed class AgentArgs
    {
        /// <summary>
        /// 任务名/中文别名 → ESchoolMission 底层值。全部用枚举成员引用（编译期校验），
        /// 枚举值变化时此处编译报错而非静默失配（0.1.15b Protocol/ESchoolMission.cs）。
        /// </summary>
        private static readonly Dictionary<string, int> AliasMap =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "morse", (int)ESchoolMission.ScMorseCode }, { "摩斯", (int)ESchoolMission.ScMorseCode }, { "摩斯密码", (int)ESchoolMission.ScMorseCode },
                { "surgery", (int)ESchoolMission.ScSurgery }, { "手术", (int)ESchoolMission.ScSurgery },
                { "spray", (int)ESchoolMission.ScMakeSpray }, { "喷雾", (int)ESchoolMission.ScMakeSpray }, { "合成喷雾", (int)ESchoolMission.ScMakeSpray },
                { "miner", (int)ESchoolMission.ScMiner }, { "矿工", (int)ESchoolMission.ScMiner },
                { "candle", (int)ESchoolMission.ScCandle }, { "蜡烛", (int)ESchoolMission.ScCandle },
                { "collector", (int)ESchoolMission.ScCollector }, { "收集", (int)ESchoolMission.ScCollector },
                { "mineralcraft", (int)ESchoolMission.ScMineralCraft }, { "放矿", (int)ESchoolMission.ScMineralCraft },
                { "craft", (int)ESchoolMission.ScCraft }, { "合成", (int)ESchoolMission.ScCraft },
                { "essence", (int)ESchoolMission.ScEssence }, { "分离", (int)ESchoolMission.ScEssence },
                { "microscope", (int)ESchoolMission.ScMicroscope }, { "显微镜", (int)ESchoolMission.ScMicroscope },
                { "bookrune", (int)ESchoolMission.ScBookRune }, { "符文书", (int)ESchoolMission.ScBookRune },
                { "rune", (int)ESchoolMission.ScRune }, { "符文", (int)ESchoolMission.ScRune },
                { "warp", (int)ESchoolMission.ScWarp }, { "传送", (int)ESchoolMission.ScWarp },
                { "boiler", (int)ESchoolMission.ScBoiler }, { "制冰", (int)ESchoolMission.ScBoiler }, { "锅炉", (int)ESchoolMission.ScBoiler },
                { "drink", (int)ESchoolMission.ScDrink }, { "调酒", (int)ESchoolMission.ScDrink },
                { "shaker", (int)ESchoolMission.ScShakeShaker }, { "摇酒", (int)ESchoolMission.ScShakeShaker },
                { "fixpc", (int)ESchoolMission.ScFixPc }, { "修电脑", (int)ESchoolMission.ScFixPc }, { "电脑", (int)ESchoolMission.ScFixPc },
                { "manikin", (int)ESchoolMission.ScManikinStart }, { "人体模型", (int)ESchoolMission.ScManikinStart }, { "假人", (int)ESchoolMission.ScManikinStart },
                { "spraycancer", (int)ESchoolMission.ScSprayCancer }, { "杀菌", (int)ESchoolMission.ScSprayCancer },
                { "potionalchemist", (int)ESchoolMission.ScPotionAlchemist }, { "炼金药", (int)ESchoolMission.ScPotionAlchemist },
                { "shakerdrink", (int)ESchoolMission.ScShakerDrink }, { "献酒", (int)ESchoolMission.ScShakerDrink },
                { "charge", (int)ESchoolMission.ScChargeBattery }, { "充电", (int)ESchoolMission.ScChargeBattery },
                { "battery", (int)ESchoolMission.ScBattery }, { "满电", (int)ESchoolMission.ScBattery },
                { "batteryminer", (int)ESchoolMission.ScBatteryMiner }, { "矿工电池", (int)ESchoolMission.ScBatteryMiner },
                { "batterywarp", (int)ESchoolMission.ScBatteryWarp }, { "传送电池", (int)ESchoolMission.ScBatteryWarp },
                { "batterybio", (int)ESchoolMission.ScBatteryBio }, { "生物电池", (int)ESchoolMission.ScBatteryBio },
                { "mushroom", (int)ESchoolMission.ScMushroom }, { "蘑菇", (int)ESchoolMission.ScMushroom },
                { "mushroomalchemist", (int)ESchoolMission.ScMushroomAlchemist }, { "蘑菇炼金", (int)ESchoolMission.ScMushroomAlchemist },
                { "nintendo", (int)ESchoolMission.ScNintendo }, { "任天堂", (int)ESchoolMission.ScNintendo },
                { "potion", (int)ESchoolMission.ScPotion }, { "药剂", (int)ESchoolMission.ScPotion },
                { "fire", (int)ESchoolMission.ScFire }, { "火焰", (int)ESchoolMission.ScFire },
                { "fish", (int)ESchoolMission.ScAquaticCapture }, { "钓鱼", (int)ESchoolMission.ScAquaticCapture },
            };

        public bool IsStop { get; private set; }
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
                return true;   // 全清 = 无过滤（Filter 保持 default），与无参预览同语义
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
