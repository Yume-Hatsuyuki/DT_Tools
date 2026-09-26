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

        // ── 道具别名表 ──────────────────────────────────────
        // 原则：Define.cs 中 ITEM_ID_* 全量收录（ITEM_ID_START=1000 是区间哨兵值，非真实道具，排除）；
        // 另收录 ITEM_SMAHO=4001（不以 ITEM_ID_ 命名，但原版扫描/开平板时确实作为手持物显示）。
        // 所有值均为 DataId，可直接传入 ItemManager.CreateAndInsertInven。
        // 常量核对：0.1.15b Define.cs:606-758。
        private static readonly Dictionary<string, int> ItemAliases =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // ── 任务/设备道具 (10xx) ──
            { "usb",           Define.ITEM_ID_USB },
            { "manikin",       Define.ITEM_ID_MANIKIN },
            { "surgerymanikin",Define.ITEM_ID_SURGERY_MANIKIN },
            { "battery_empty", Define.ITEM_ID_BATTERY_EMPTY },
            { "batteryempty",  Define.ITEM_ID_BATTERY_EMPTY },
            { "battery_full",  Define.ITEM_ID_BATTERY_FULL },
            { "batteryfull",   Define.ITEM_ID_BATTERY_FULL },
            { "battery",       Define.ITEM_ID_BATTERY_FULL },   // 默认满电
            // ── 花 (10xx) ──
            { "flower_red",    Define.ITEM_ID_RED_FLOWER },
            { "redflower",     Define.ITEM_ID_RED_FLOWER },
            { "flower_blue",   Define.ITEM_ID_BLUE_FLOWER },
            { "blueflower",    Define.ITEM_ID_BLUE_FLOWER },
            { "flower_yellow", Define.ITEM_ID_YELLOW_FLOWER },
            { "yellowflower",  Define.ITEM_ID_YELLOW_FLOWER },
            { "flower_pink",   Define.ITEM_ID_PINK_FLOWER },
            { "pinkflower",    Define.ITEM_ID_PINK_FLOWER },
            { "flower",        Define.ITEM_ID_RED_FLOWER },     // 默认红花
            // ── 通用消耗/采集道具 (10xx) ──
            { "ample",         Define.ITEM_ID_AMPLE },          // 安瓿瓶
            { "mushroom",      Define.ITEM_ID_MUSHROOM },
            { "icewater",      Define.ITEM_ID_ICE_WATER },
            { "water",         Define.ITEM_ID_ICE_WATER },
            { "ultimatepotion",Define.ITEM_ID_ULTIMATEPOTION },
            { "ultimate",      Define.ITEM_ID_ULTIMATEPOTION },
            { "pickaxe",       Define.ITEM_ID_PICKAXE },
            // ── 矿石 (10xx) ──
            { "bluemineral",   Define.ITEM_ID_BLUEMINERAL },
            { "greenmineral",  Define.ITEM_ID_GREENMINERAL },
            { "redmineral",    Define.ITEM_ID_REDMINERAL },
            { "essence",       Define.ITEM_ID_ESSENCE },
            // ── 手摇球 (10xx) ──
            { "shaker",        Define.ITEM_ID_SHAKER_BALL_01 },
            { "shaker01",      Define.ITEM_ID_SHAKER_BALL_01 },
            { "shaker02",      Define.ITEM_ID_SHAKER_BALL_02 },
            // ── 注射器 (10xx) ──
            { "syringe_empty", Define.ITEM_ID_SYRINGE_EMPTY },
            { "syringe",       Define.ITEM_ID_SYRINGE_EMPTY },
            { "syringe_red",   Define.ITEM_ID_SYRINGE_RED },
            { "syringe_green", Define.ITEM_ID_SYRINGE_GREEN },
            { "syringe_blue",  Define.ITEM_ID_SYRINGE_BLUE },
            { "syringe_yellow",Define.ITEM_ID_SYRINGE_YELLOW },
            // ── 药水 (10xx) ──
            { "potion_red",    Define.ITEM_ID_POTION_RED },
            { "potionred",     Define.ITEM_ID_POTION_RED },
            { "potion_green",  Define.ITEM_ID_POTION_GREEN },
            { "potiongreen",   Define.ITEM_ID_POTION_GREEN },
            { "potion_blue",   Define.ITEM_ID_POTION_BLUE },
            { "potionblue",    Define.ITEM_ID_POTION_BLUE },
            { "potion_yellow", Define.ITEM_ID_POTION_YELLOW },
            { "potionyellow",  Define.ITEM_ID_POTION_YELLOW },
            { "potion",        Define.ITEM_ID_POTION_RED },     // 默认红药水
            // ── 书 (10xx) ──
            { "book_red",      Define.ITEM_ID_RED_BOOK },
            { "redbook",       Define.ITEM_ID_RED_BOOK },
            { "book_blue",     Define.ITEM_ID_BLUE_BOOK },
            { "bluebook",      Define.ITEM_ID_BLUE_BOOK },
            { "book_green",    Define.ITEM_ID_GREEN_BOOK },
            { "greenbook",     Define.ITEM_ID_GREEN_BOOK },
            { "book_yellow",   Define.ITEM_ID_YELLOW_BOOK },
            { "yellowbook",    Define.ITEM_ID_YELLOW_BOOK },
            { "book",          Define.ITEM_ID_RED_BOOK },       // 默认红书
            // ── 钓鱼 (10xx) ──
            { "rod",           Define.ITEM_ID_FISHING_ROD },
            { "fishingrod",    Define.ITEM_ID_FISHING_ROD },
            { "fish",          Define.ITEM_ID_FISH_NORMAL },
            { "fish_normal",   Define.ITEM_ID_FISH_NORMAL },
            { "fish_rare",     Define.ITEM_ID_FISH_RARE },
            { "fishrare",      Define.ITEM_ID_FISH_RARE },
            { "fish_gold",     Define.ITEM_ID_FISH_GOLD },
            { "fishgold",      Define.ITEM_ID_FISH_GOLD },
            // ── 奖杯 (10xx，大厅可挥动触发击退) ──
            { "trophy_gold",   Define.ITEM_ID_TROPHY_GOLD },
            { "trophy",        Define.ITEM_ID_TROPHY_GOLD },
            { "gold",          Define.ITEM_ID_TROPHY_GOLD },
            { "trophy_silver", Define.ITEM_ID_TROPHY_SILVER },
            { "silver",        Define.ITEM_ID_TROPHY_SILVER },
            { "trophy_bronze", Define.ITEM_ID_TROPHY_BRONZE },
            { "bronze",        Define.ITEM_ID_TROPHY_BRONZE },
            // ── 武器 (2xxx，与游戏内攻击动作使用的是同一手持槽位) ──
            { "knife",         Define.ITEM_ID_KNIFE },
            { "bat",           Define.ITEM_ID_BAT },
            { "hammer",        Define.ITEM_ID_HAMMER },
            { "shovel",        Define.ITEM_ID_SHOVEL },
            { "accordion",     Define.ITEM_ID_ACCORDION },
            // ── 饮料罐 (3xxx) ──
            { "can01",         Define.ITEM_ID_CAN01 },
            { "can1",          Define.ITEM_ID_CAN01 },
            { "can02",         Define.ITEM_ID_CAN02 },
            { "can2",          Define.ITEM_ID_CAN02 },
            { "can03",         Define.ITEM_ID_CAN03 },
            { "can3",          Define.ITEM_ID_CAN03 },
            { "can04",         Define.ITEM_ID_CAN04 },
            { "can4",          Define.ITEM_ID_CAN04 },
            { "can05",         Define.ITEM_ID_CAN05 },
            { "can5",          Define.ITEM_ID_CAN05 },
            // ── 大厅专用道具 (3xxx) ──
            { "adrenaline",    Define.ITEM_ID_ADRENALINE },
            { "adren",         Define.ITEM_ID_ADRENALINE },
            { "toyhammer",     Define.ITEM_ID_TOYHAMMER },
            { "airhorn",       Define.ITEM_ID_AIRHORN },
            { "horn",          Define.ITEM_ID_AIRHORN },
            { "bell",          Define.ITEM_ID_BELL },
            { "syringe_potion",Define.ITEM_ID_SYRINGE_POTION },
            // ── 手机/平板 (4xxx；原版为扫描/打开平板时的虚拟手持物) ──
            { "smaho",         Define.ITEM_SMAHO },
            { "phone",         Define.ITEM_SMAHO },
            { "tablet",        Define.ITEM_SMAHO },
            // ── 灯笼 (4xxx) ──
            { "lantern",       Define.ITEM_ID_LANTERN },
            { "lantern_red",   Define.ITEM_ID_LANTERN_RED },
            { "lantern_blue",  Define.ITEM_ID_LANTERN_BLUE },
            // ── 特殊 (5xxx) ──
            { "question_flower", Define.ITEM_ID_QUESTION_FLOWER },
            { "questionflower",  Define.ITEM_ID_QUESTION_FLOWER },
            // ── 清空 ──
            { "none",          0 },
            { "clear",         0 },
            { "empty",         0 },
        };

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
