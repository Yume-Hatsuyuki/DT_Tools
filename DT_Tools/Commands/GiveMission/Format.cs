using System.Linq;
using System.Text;
using Protocol;

namespace DT_Tools.Commands.GiveMission
{
    /// <summary>/givemission 输出格式化：可派发任务列表 + 人类回复 + JSON 结果 DTO。</summary>
    internal static class GiveMissionFormat
    {
        public static string MissionList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("━━━ 可派发任务（对照 Protocol.ESchoolMission 全量收录） ━━━");
            sb.AppendLine(" 说明: 派发链尾任务会自动联动初始化前置设备(不计前置分)，");
            sb.AppendLine("       只对用户指定任务计分。建议直接派发链尾任务。");
            sb.AppendLine(" 【手术/医疗线】");
            sb.AppendLine("  ScManikinStart(18)     假人启动（链头，ShouldEnqueue）");
            sb.AppendLine("  ScSurgery(1)          手术（←联动 18）");
            sb.AppendLine("  ScMakeSpray(2)         制喷雾（链头，ShouldEnqueue）");
            sb.AppendLine("  ScSprayCancer(20)     喷癌症（←联动 2）");
            sb.AppendLine("  ScMakeMushroom(28)    制蘑菇（链头，ShouldEnqueue）");
            sb.AppendLine("  ScMushroomAlchemist(30) 蘑菇炼金（←联动 28）");
            sb.AppendLine("  ScPotionAlchemist(21) 药剂炼金（←联动 30→28）");
            sb.AppendLine("  ScPotion(35)           药剂（PotionScanner 启动）");
            sb.AppendLine(" 【电气/电池链】");
            sb.AppendLine("  ScChargeBattery(23)   充电（链头，扣分任务 Point=-3）");
            sb.AppendLine("  ScBatteryMiner(25)    电池矿工（硬前置 23，NextType=3）");
            sb.AppendLine("  ScMiner(3)             矿工（←联动 25→23）");
            sb.AppendLine("  ScBatteryWarp(26)     电池传送（硬前置 23，NextType=13）");
            sb.AppendLine("  ScWarp(13)             传送（←联动 26→23）");
            sb.AppendLine("  ScBatteryBio(27)      电池生物（硬前置 23）");
            sb.AppendLine("  ScBoiler(14)          锅炉（全部 Boiler.StartMission）");
            sb.AppendLine(" 【矿物/合成线】");
            sb.AppendLine("  ScMineralCraft(7)     矿物合成（链头，ShouldEnqueue）");
            sb.AppendLine("  ScCraft(8)            合成（←联动 7）");
            sb.AppendLine("  ScCollector(6)        收集器（Collector.StartMission）");
            sb.AppendLine("  ScEssence(9)          精华（SubType==0 的 Sample 启动）");
            sb.AppendLine("  ScMicroscope(10)     显微镜（SubType==1 的 Sample 启动）");
            sb.AppendLine(" 【神秘学线】");
            sb.AppendLine("  ScBookRune(11)        符文之书（链头，ShouldEnqueue，生成符文书）");
            sb.AppendLine("  ScRune(12)            符文（←联动 11，自动生成符文书）");
            sb.AppendLine("  ScCandle(4)           蜡烛（启动 OccultList + 设置书本提示）");
            sb.AppendLine("  ScFire(36)            火（OccultFire.StartFireMission）");
            sb.AppendLine(" 【饮品类】");
            sb.AppendLine("  ScDrink(15)           饮品（链头，ShouldEnqueue）");
            sb.AppendLine("  ScShakeShaker(16)    摇摇杯（←联动 15）");
            sb.AppendLine("  ScShakerDrink(22)    摇杯饮品（←联动 16→15）");
            sb.AppendLine(" 【其他】");
            sb.AppendLine("  ScFixPc(17)           修电脑（插入 1008 物品 + Computer 状态变更）");
            sb.AppendLine("  ScNintendo(34)       任天堂（任天堂游戏机 + 洗牌 CoinOrder）");
            sb.AppendLine("  ScMorseCode(37)      摩斯密码（随机 Morse 设备启动）");
            sb.AppendLine("  ScAquaticCapture(40) 钓鱼（随机 Fishing 设备启动）");
            sb.AppendLine(" 【占位任务（switch 无分支，派发后不激活设备）】");
            sb.AppendLine("  ScBattery(24) / ScMushroom(29) / ScMushroomBio(31) / ScBatteryMiner2(32)");
            sb.AppendLine("  ScMiner2(33) / ScFusebox(38) / ScWeapon(39)");
            sb.AppendLine("【派发示例】");
            sb.AppendLine("  /givemission ScRune        ← 自动联动 ScBookRune 生成符文书");
            sb.AppendLine("  /givemission 12             ← 数字");
            sb.AppendLine("  /givemission 符文            ← 中文别名");
            sb.AppendLine("  /givemission ScShakerDrink  ← 联动 15→16 初始化整条饮品线");
            return sb.ToString();
        }

        public static string Reply(GiveMissionLogic.Outcome outcome)
        {
            var sb = new StringBuilder();
            sb.Append($"已派发任务 {outcome.Mission}({(int)outcome.Mission})");
            if (outcome.InitializedPrereqs.Count > 0)
            {
                var names = outcome.InitializedPrereqs.Select(t => $"{(ESchoolMission)t}({t})");
                sb.Append($"（已联动初始化前置设备: {string.Join(" → ", names)}）");
            }
            if (outcome.PrevType != 0) sb.Append($"（已联动启动硬前置 Type={outcome.PrevType}）");
            if (outcome.IsEmptyCase)
                sb.Append("。注意：该任务在原版 switch 中无 Start_ScXXX 分支，派发后无设备激活，仅占位");
            sb.Append("。");
            return sb.ToString();
        }

        public static object Result(GiveMissionLogic.Outcome outcome)
            => new
            {
                mission = outcome.Mission.ToString(),
                mid = (int)outcome.Mission,
                initialized = outcome.InitializedPrereqs,
                prev_type = outcome.PrevType,
                empty_case = outcome.IsEmptyCase,
            };
    }
}
