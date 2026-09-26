using System;
using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Commands.GiveMission
{
    /// <summary>
    /// /givemission 参数：任务类型（枚举名大小写不敏感 / 数字 / 中文别名）。
    /// 枚举值核对：0.1.15b Protocol/ESchoolMission.cs（ScNone=0 … ScAquaticCapture=40）。
    /// </summary>
    internal static class GiveMissionArgs
    {
        private static readonly Dictionary<string, ESchoolMission> MissionAliases =
            new Dictionary<string, ESchoolMission>(StringComparer.OrdinalIgnoreCase)
            {
                { "手术",       ESchoolMission.ScSurgery },
                { "制喷雾",     ESchoolMission.ScMakeSpray },
                { "矿工",       ESchoolMission.ScMiner },
                { "蜡烛",       ESchoolMission.ScCandle },
                { "收集器",     ESchoolMission.ScCollector },
                { "矿物合成",   ESchoolMission.ScMineralCraft },
                { "合成",       ESchoolMission.ScCraft },
                { "精华",       ESchoolMission.ScEssence },
                { "显微镜",     ESchoolMission.ScMicroscope },
                { "符文之书",   ESchoolMission.ScBookRune },
                { "符文",       ESchoolMission.ScRune },
                { "传送",       ESchoolMission.ScWarp },
                { "锅炉",       ESchoolMission.ScBoiler },
                { "饮品",       ESchoolMission.ScDrink },
                { "摇摇杯",     ESchoolMission.ScShakeShaker },
                { "修电脑",     ESchoolMission.ScFixPc },
                { "假人启动",   ESchoolMission.ScManikinStart },
                { "喷癌症",     ESchoolMission.ScSprayCancer },
                { "药剂炼金",   ESchoolMission.ScPotionAlchemist },
                { "摇杯饮品",   ESchoolMission.ScShakerDrink },
                { "充电",       ESchoolMission.ScChargeBattery },
                { "电池矿工",   ESchoolMission.ScBatteryMiner },
                { "电池传送",   ESchoolMission.ScBatteryWarp },
                { "电池生物",   ESchoolMission.ScBatteryBio },
                { "制蘑菇",     ESchoolMission.ScMakeMushroom },
                { "蘑菇炼金",   ESchoolMission.ScMushroomAlchemist },
                { "任天堂",     ESchoolMission.ScNintendo },
                { "药剂",       ESchoolMission.ScPotion },
                { "火",         ESchoolMission.ScFire },
                { "摩斯密码",   ESchoolMission.ScMorseCode },
                { "钓鱼",       ESchoolMission.ScAquaticCapture },
            };

        public static bool TryParseMission(string s, out ESchoolMission missionType)
        {
            if (MissionAliases.TryGetValue(s, out missionType)) return true;
            return Enum.TryParse(s, ignoreCase: true, out missionType)
                   && Enum.IsDefined(typeof(ESchoolMission), missionType);
        }
    }
}
