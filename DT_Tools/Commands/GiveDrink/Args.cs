using System;
using System.Collections.Generic;

namespace DT_Tools.Commands.GiveDrink
{
    /// <summary>
    /// /givedrink 参数：&lt;all|#id&gt; [item]。目标缺省 all，道具缺省 can01；
    /// 第一参不是 all / #id 时视为道具名（即 "/givedrink knife" = 全体发匕首）。
    /// </summary>
    internal sealed class GiveDrinkArgs
    {
        public bool All { get; private set; }
        public int PlayerId { get; private set; }
        public int ItemId { get; private set; }

        /// <summary>
        /// 道具定义条目：唯一数据源——别名解析（TryParseItem）与无参道具列表
        /// （GiveDrinkFormat.ItemList）都由它生成，消除双份表口径漂移
        /// （历史上出现过 shaker 1039 双名、icewater 1028 热水/冰水两种写法）。
        /// </summary>
        internal sealed class ItemDef
        {
            public string[] Aliases;   // 首个为主别名（列表展示用），其余为等价别名
            public int Id;             // DataId（0=清空手持物）
            public string Label;       // 中文说明
            public string Group;       // 分组标题
        }

        // 原则：Define.cs 中 ITEM_ID_* 全量收录（ITEM_ID_START=1000 是区间哨兵值，非真实道具，排除）；
        // 另收录 ITEM_SMAHO=4001（不以 ITEM_ID_ 命名，但原版扫描/开平板时确实作为手持物显示）。
        // 所有值均为 DataId，可直接传入 ItemManager.CreateAndInsertInven。常量核对：0.1.15b Define.cs:606-758。
        internal static readonly List<ItemDef> ItemDefs = new List<ItemDef>
        {
            // ── 任务/设备道具 (10xx) ──
            new ItemDef { Aliases = new[]{ "usb" }, Id = Define.ITEM_ID_USB, Label = "U盘", Group = "任务/设备道具" },
            new ItemDef { Aliases = new[]{ "manikin" }, Id = Define.ITEM_ID_MANIKIN, Label = "人体模型", Group = "任务/设备道具" },
            new ItemDef { Aliases = new[]{ "surgerymanikin" }, Id = Define.ITEM_ID_SURGERY_MANIKIN, Label = "手术人体模型", Group = "任务/设备道具" },
            new ItemDef { Aliases = new[]{ "battery_empty", "batteryempty" }, Id = Define.ITEM_ID_BATTERY_EMPTY, Label = "空电池", Group = "任务/设备道具" },
            new ItemDef { Aliases = new[]{ "battery_full", "batteryfull", "battery" }, Id = Define.ITEM_ID_BATTERY_FULL, Label = "满电池（battery 默认）", Group = "任务/设备道具" },
            // ── 花 (10xx) ──
            new ItemDef { Aliases = new[]{ "flower_red", "redflower" }, Id = Define.ITEM_ID_RED_FLOWER, Label = "红花", Group = "花" },
            new ItemDef { Aliases = new[]{ "flower_blue", "blueflower" }, Id = Define.ITEM_ID_BLUE_FLOWER, Label = "蓝花", Group = "花" },
            new ItemDef { Aliases = new[]{ "flower_yellow", "yellowflower" }, Id = Define.ITEM_ID_YELLOW_FLOWER, Label = "黄花", Group = "花" },
            new ItemDef { Aliases = new[]{ "flower_pink", "pinkflower" }, Id = Define.ITEM_ID_PINK_FLOWER, Label = "粉花", Group = "花" },
            new ItemDef { Aliases = new[]{ "flower" }, Id = Define.ITEM_ID_RED_FLOWER, Label = "花（默认红花）", Group = "花" },
            // ── 通用消耗/采集道具 (10xx) ──
            new ItemDef { Aliases = new[]{ "ample" }, Id = Define.ITEM_ID_AMPLE, Label = "安瓿瓶", Group = "消耗/采集道具" },
            new ItemDef { Aliases = new[]{ "mushroom" }, Id = Define.ITEM_ID_MUSHROOM, Label = "蘑菇", Group = "消耗/采集道具" },
            new ItemDef { Aliases = new[]{ "icewater", "water" }, Id = Define.ITEM_ID_ICE_WATER, Label = "冰水/热水（调酒台投料）", Group = "消耗/采集道具" },
            new ItemDef { Aliases = new[]{ "ultimatepotion", "ultimate" }, Id = Define.ITEM_ID_ULTIMATEPOTION, Label = "终极药水", Group = "消耗/采集道具" },
            new ItemDef { Aliases = new[]{ "pickaxe" }, Id = Define.ITEM_ID_PICKAXE, Label = "十字镐", Group = "消耗/采集道具" },
            // ── 矿石 (10xx) ──
            new ItemDef { Aliases = new[]{ "bluemineral" }, Id = Define.ITEM_ID_BLUEMINERAL, Label = "蓝矿石", Group = "矿石" },
            new ItemDef { Aliases = new[]{ "greenmineral" }, Id = Define.ITEM_ID_GREENMINERAL, Label = "绿矿石", Group = "矿石" },
            new ItemDef { Aliases = new[]{ "redmineral" }, Id = Define.ITEM_ID_REDMINERAL, Label = "红矿石", Group = "矿石" },
            new ItemDef { Aliases = new[]{ "essence" }, Id = Define.ITEM_ID_ESSENCE, Label = "精华", Group = "矿石" },
            // ── 手摇球 (10xx) ──
            new ItemDef { Aliases = new[]{ "shaker", "shaker01" }, Id = Define.ITEM_ID_SHAKER_BALL_01, Label = "手摇球·生酒（玩家摇动变 1040）", Group = "手摇球" },
            new ItemDef { Aliases = new[]{ "shaker02" }, Id = Define.ITEM_ID_SHAKER_BALL_02, Label = "手摇球·熟酒", Group = "手摇球" },
            // ── 注射器 (10xx) ──
            new ItemDef { Aliases = new[]{ "syringe_empty", "syringe" }, Id = Define.ITEM_ID_SYRINGE_EMPTY, Label = "空注射器（syringe 默认）", Group = "注射器" },
            new ItemDef { Aliases = new[]{ "syringe_red" }, Id = Define.ITEM_ID_SYRINGE_RED, Label = "红注射器", Group = "注射器" },
            new ItemDef { Aliases = new[]{ "syringe_green" }, Id = Define.ITEM_ID_SYRINGE_GREEN, Label = "绿注射器", Group = "注射器" },
            new ItemDef { Aliases = new[]{ "syringe_blue" }, Id = Define.ITEM_ID_SYRINGE_BLUE, Label = "蓝注射器", Group = "注射器" },
            new ItemDef { Aliases = new[]{ "syringe_yellow" }, Id = Define.ITEM_ID_SYRINGE_YELLOW, Label = "黄注射器", Group = "注射器" },
            // ── 药水 (10xx) ──
            new ItemDef { Aliases = new[]{ "potion_red", "potionred" }, Id = Define.ITEM_ID_POTION_RED, Label = "红药水", Group = "药水" },
            new ItemDef { Aliases = new[]{ "potion_green", "potiongreen" }, Id = Define.ITEM_ID_POTION_GREEN, Label = "绿药水", Group = "药水" },
            new ItemDef { Aliases = new[]{ "potion_blue", "potionblue" }, Id = Define.ITEM_ID_POTION_BLUE, Label = "蓝药水", Group = "药水" },
            new ItemDef { Aliases = new[]{ "potion_yellow", "potionyellow" }, Id = Define.ITEM_ID_POTION_YELLOW, Label = "黄药水", Group = "药水" },
            new ItemDef { Aliases = new[]{ "potion" }, Id = Define.ITEM_ID_POTION_RED, Label = "药水（默认红）", Group = "药水" },
            // ── 书 (10xx) ──
            new ItemDef { Aliases = new[]{ "book_red", "redbook" }, Id = Define.ITEM_ID_RED_BOOK, Label = "红书", Group = "书" },
            new ItemDef { Aliases = new[]{ "book_blue", "bluebook" }, Id = Define.ITEM_ID_BLUE_BOOK, Label = "蓝书", Group = "书" },
            new ItemDef { Aliases = new[]{ "book_green", "greenbook" }, Id = Define.ITEM_ID_GREEN_BOOK, Label = "绿书", Group = "书" },
            new ItemDef { Aliases = new[]{ "book_yellow", "yellowbook" }, Id = Define.ITEM_ID_YELLOW_BOOK, Label = "黄书", Group = "书" },
            new ItemDef { Aliases = new[]{ "book" }, Id = Define.ITEM_ID_RED_BOOK, Label = "书（默认红书）", Group = "书" },
            // ── 钓鱼 (10xx) ──
            new ItemDef { Aliases = new[]{ "rod", "fishingrod" }, Id = Define.ITEM_ID_FISHING_ROD, Label = "鱼竿", Group = "钓鱼" },
            new ItemDef { Aliases = new[]{ "fish", "fish_normal" }, Id = Define.ITEM_ID_FISH_NORMAL, Label = "普通鱼（fish 默认）", Group = "钓鱼" },
            new ItemDef { Aliases = new[]{ "fish_rare", "fishrare" }, Id = Define.ITEM_ID_FISH_RARE, Label = "稀有鱼", Group = "钓鱼" },
            new ItemDef { Aliases = new[]{ "fish_gold", "fishgold" }, Id = Define.ITEM_ID_FISH_GOLD, Label = "金鱼", Group = "钓鱼" },
            // ── 奖杯 (大厅可挥动触发击退) ──
            new ItemDef { Aliases = new[]{ "trophy_gold", "trophy", "gold" }, Id = Define.ITEM_ID_TROPHY_GOLD, Label = "金奖杯（trophy/gold 默认）", Group = "奖杯" },
            new ItemDef { Aliases = new[]{ "trophy_silver", "silver" }, Id = Define.ITEM_ID_TROPHY_SILVER, Label = "银奖杯", Group = "奖杯" },
            new ItemDef { Aliases = new[]{ "trophy_bronze", "bronze" }, Id = Define.ITEM_ID_TROPHY_BRONZE, Label = "铜奖杯", Group = "奖杯" },
            // ── 武器 (2xxx，与游戏内攻击动作使用的是同一手持槽位) ──
            new ItemDef { Aliases = new[]{ "knife" }, Id = Define.ITEM_ID_KNIFE, Label = "匕首", Group = "武器" },
            new ItemDef { Aliases = new[]{ "bat" }, Id = Define.ITEM_ID_BAT, Label = "球棒", Group = "武器" },
            new ItemDef { Aliases = new[]{ "hammer" }, Id = Define.ITEM_ID_HAMMER, Label = "锤子", Group = "武器" },
            new ItemDef { Aliases = new[]{ "shovel" }, Id = Define.ITEM_ID_SHOVEL, Label = "铁锹", Group = "武器" },
            new ItemDef { Aliases = new[]{ "accordion" }, Id = Define.ITEM_ID_ACCORDION, Label = "手风琴", Group = "武器" },
            // ── 饮料罐 (3xxx) ──
            new ItemDef { Aliases = new[]{ "can01", "can1" }, Id = Define.ITEM_ID_CAN01, Label = "饮料罐 01", Group = "饮料罐" },
            new ItemDef { Aliases = new[]{ "can02", "can2" }, Id = Define.ITEM_ID_CAN02, Label = "饮料罐 02", Group = "饮料罐" },
            new ItemDef { Aliases = new[]{ "can03", "can3" }, Id = Define.ITEM_ID_CAN03, Label = "饮料罐 03", Group = "饮料罐" },
            new ItemDef { Aliases = new[]{ "can04", "can4" }, Id = Define.ITEM_ID_CAN04, Label = "饮料罐 04", Group = "饮料罐" },
            new ItemDef { Aliases = new[]{ "can05", "can5" }, Id = Define.ITEM_ID_CAN05, Label = "饮料罐 05", Group = "饮料罐" },
            // ── 大厅专用道具 (3xxx) ──
            new ItemDef { Aliases = new[]{ "adrenaline", "adren" }, Id = Define.ITEM_ID_ADRENALINE, Label = "肾上腺素", Group = "大厅专用道具" },
            new ItemDef { Aliases = new[]{ "toyhammer" }, Id = Define.ITEM_ID_TOYHAMMER, Label = "玩具锤", Group = "大厅专用道具" },
            new ItemDef { Aliases = new[]{ "airhorn", "horn" }, Id = Define.ITEM_ID_AIRHORN, Label = "气喇叭", Group = "大厅专用道具" },
            new ItemDef { Aliases = new[]{ "bell" }, Id = Define.ITEM_ID_BELL, Label = "铃铛", Group = "大厅专用道具" },
            new ItemDef { Aliases = new[]{ "syringe_potion" }, Id = Define.ITEM_ID_SYRINGE_POTION, Label = "注射药水", Group = "大厅专用道具" },
            // ── 手机/平板 (4xxx；原版为扫描/打开平板时的虚拟手持物) ──
            new ItemDef { Aliases = new[]{ "smaho", "phone", "tablet" }, Id = Define.ITEM_SMAHO, Label = "手机", Group = "手机/平板" },
            // ── 灯笼 (4xxx) ──
            new ItemDef { Aliases = new[]{ "lantern" }, Id = Define.ITEM_ID_LANTERN, Label = "白灯笼", Group = "灯笼" },
            new ItemDef { Aliases = new[]{ "lantern_red" }, Id = Define.ITEM_ID_LANTERN_RED, Label = "红灯笼", Group = "灯笼" },
            new ItemDef { Aliases = new[]{ "lantern_blue" }, Id = Define.ITEM_ID_LANTERN_BLUE, Label = "蓝灯笼", Group = "灯笼" },
            // ── 特殊 (5xxx) ──
            new ItemDef { Aliases = new[]{ "question_flower", "questionflower" }, Id = Define.ITEM_ID_QUESTION_FLOWER, Label = "问号花", Group = "特殊" },
        };

        /// <summary>清空语义（0=清空手持物），不进列表主体、由列表脚注展示。</summary>
        private static readonly Dictionary<string, int> ClearAliases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "none", 0 }, { "clear", 0 }, { "empty", 0 },
        };

        private static readonly Dictionary<string, int> ItemAliases = BuildAliasMap();

        private static Dictionary<string, int> BuildAliasMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in ItemDefs)
            {
                foreach (var alias in def.Aliases)
                    map[alias] = def.Id;
            }
            foreach (var kv in ClearAliases)
                map[kv.Key] = kv.Value;
            return map;
        }

        public static bool TryParseItem(string s, out int itemId)
        {
            if (ItemAliases.TryGetValue(s, out itemId)) return true;
            return int.TryParse(s, out itemId);
        }

        public static bool TryParse(string[] args, out GiveDrinkArgs parsed, out string error)
        {
            parsed = new GiveDrinkArgs
            {
                All = true,
                ItemId = Define.ITEM_ID_CAN01,
            };

            int index = 0;
            if (args.Length > 0)
            {
                string target = args[0];
                if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    index = 1;
                }
                else if (target.StartsWith("#") && int.TryParse(target.Substring(1), out int pid))
                {
                    parsed.All = false;
                    parsed.PlayerId = pid;
                    index = 1;
                }
                // 否则视为 item 参数，target 保持 all
            }

            if (index < args.Length)
            {
                string itemArg = args[index];
                if (!TryParseItem(itemArg, out int itemId))
                {
                    error = $"未知道具: {itemArg}（直接输入 /givedrink 查看可用列表）";
                    return false;
                }
                parsed.ItemId = itemId;
            }

            error = null;
            return true;
        }
    }
}
