using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Protocol;
using Server.Game;

namespace DT_Tools.Console.Commands
{
    /// <summary>
    /// /givedrink &lt;all|#id&gt; &lt;item&gt;
    ///
    /// 不带参数时打印可用道具列表，不执行发放。
    ///
    /// 目标:
    ///   all          全体玩家
    ///   #&lt;playerId&gt;  指定玩家数字 ID
    ///
    /// 道具收录范围: Define.cs 中全部 ITEM_ID_* 常量（ITEM_ID_START 哨兵值除外）。
    /// EquipItem(int itemId) 对任意 ID 一视同仁地贴 Sprite，武器(2xxx)与任务道具
    /// 和饮料罐走的是同一条渲染路径，没有类型限制，因此全部可发。
    ///
    /// 示例:
    ///   /givedrink all can03   → 全体 can03
    ///   /givedrink #5 knife    → 玩家5 匕首
    ///   /givedrink all 0       → 清空全体手持物
    /// </summary>
    internal sealed class GiveDrinkCommand : IConsoleCommand
    {
        public string   Name        => "givedrink";
        public string[] Aliases     => new[] { "drink", "give" };
        public string   Usage       => "givedrink <all|#id> <item>";
        public string   Description => "给所有/指定玩家发放手持道具（含武器与任务道具）。不带参数时显示可用道具列表。";

        // ── 道具别名表 ──────────────────────────────────────
        // 原则：Define.cs 中 ITEM_ID_* 全量收录（ITEM_ID_START=1000 是区间哨兵值，非真实道具，排除）。
        // EquipItem(int itemId) 对任意 itemId 一视同仁：只要 Managers.Data.ItemDic 里能查到，
        // 就按 AnimType 贴到 item / twohanded / phone 槽位，武器(2xxx)与任务道具走的是同一条路径，
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

        // 与 ItemAliases 对应的人类可读说明（每类取主别名，不重复列出短名）
        private static readonly string ItemList =
            "━━━ 可用道具（对照 Define.cs 全量收录） ━━━\n" +
            " 【任务/设备道具】\n" +
            "  usb              U盘               (1008)\n" +
            "  manikin          人体模型          (1009)\n" +
            "  surgerymanikin   手术人体模型      (1010)\n" +
            "  battery_empty    空电池            (1011)\n" +
            "  battery_full     满电池            (1015)\n" +
            " 【花】\n" +
            "  flower_red/blue/yellow/pink        (1021~1024)\n" +
            " 【消耗/采集道具】\n" +
            "  ample            安瓿瓶            (1025)\n" +
            "  mushroom         蘑菇              (1026)\n" +
            "  icewater         冰水              (1028)\n" +
            "  ultimatepotion   终极药水          (1030)\n" +
            "  pickaxe          十字镐            (1031)\n" +
            "  bluemineral      蓝矿石            (1032)\n" +
            "  greenmineral     绿矿石            (1033)\n" +
            "  redmineral       红矿石            (1034)\n" +
            "  essence          精华              (1035)\n" +
            "  shaker01/02      手摇球            (1039/1040)\n" +
            " 【注射器与药水】\n" +
            "  syringe_empty/red/green/blue/yellow(1041~1045)\n" +
            "  potion_red/green/blue/yellow       (1046~1049)\n" +
            " 【书】\n" +
            "  book_red/blue/green/yellow         (1051~1054)\n" +
            " 【钓鱼】\n" +
            "  rod              鱼竿              (1058)\n" +
            "  fish_normal      普通鱼            (1059)\n" +
            "  fish_rare        稀有鱼            (1060)\n" +
            "  fish_gold        金鱼              (1061)\n" +
            " 【奖杯 - 大厅可挥动击退】\n" +
            "  trophy_gold      金奖杯            (1062)\n" +
            "  trophy_silver    银奖杯            (1063)\n" +
            "  trophy_bronze    铜奖杯            (1064)\n" +
            " 【武器 - 与实战同一手持槽位】\n" +
            "  knife            匕首              (2001)\n" +
            "  bat              球棒              (2002)\n" +
            "  hammer           锤子              (2003)\n" +
            "  shovel           铁锹              (2004)\n" +
            "  accordion        手风琴            (2005)\n" +
            " 【饮料罐与大厅道具】\n" +
            "  can01~can05      各色饮料罐        (3001~3005)\n" +
            "  adrenaline       肾上腺素          (3006)\n" +
            "  toyhammer        玩具锤            (3007)\n" +
            "  airhorn          气喇叭            (3008)\n" +
            "  bell             铃铛              (3009)\n" +
            "  syringe_potion   注射药水          (3011)\n" +
            " 【灯笼】\n" +
            "  lantern          白灯笼            (4004)\n" +
            "  lantern_red      红灯笼            (4005)\n" +
            "  lantern_blue     蓝灯笼            (4006)\n" +
            " 【特殊】\n" +
            "  question_flower  问号花            (5001)\n" +
            "  0 / clear        清空手持物\n" +
            "  <数字>           直接指定 ITEM_ID\n" +
            "示例: /givedrink all can01   /givedrink #5 knife";

        public void Execute(string[] args, WebConsole console)
        {
            // 不带参数：仅显示可用道具列表，不执行
            if (args.Length == 0)
            {
                console.Log(ItemList, LogLevel.Info);
                return;
            }

            // 确认是房主（只有 Host 端才有 GameRoom）
            if (Managers.Host == null || !Managers.Host.IsHost)
            {
                console.Log("此命令只能由房主执行。", LogLevel.Warning);
                return;
            }

            var room = GameRoom.Instance;
            if (room == null || room.Players.Count == 0)
            {
                console.Log("当前没有活动的游戏房间或玩家。", LogLevel.Warning);
                return;
            }

            // ── 解析参数 ──────────────────────────────────
            // 默认值
            bool   targetAll = true;
            int    targetId  = -1;
            int    itemId    = Define.ITEM_ID_CAN01;

            int argIdx = 0;

            // 第一个参数：目标
            if (argIdx < args.Length)
            {
                string t = args[argIdx];
                if (t.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    targetAll = true;
                    argIdx++;
                }
                else if (t.StartsWith("#") && int.TryParse(t.Substring(1), out int pid))
                {
                    targetAll = false;
                    targetId  = pid;
                    argIdx++;
                }
                // 否则视为 item 参数，target 保持 all
            }

            // 第二个参数：道具
            if (argIdx < args.Length)
            {
                string itemArg = args[argIdx];
                if (!TryParseItem(itemArg, out itemId))
                {
                    console.Log($"未知道具: {itemArg}（直接输入 /givedrink 查看可用列表）", LogLevel.Warning);
                    return;
                }
            }

            // ── 执行广播 ──────────────────────────────────
            // GameRoom.Broadcast 已封装线程安全，直接调用即可（我们在主线程里）
            int count = 0;

            if (targetAll)
            {
                foreach (var player in room.Players)
                {
                    if (player?.PublicInfo == null) continue;
                    SendHandItem(room, player.PublicInfo.PlayerId, itemId);
                    count++;
                }
                console.Log($"已给 {count} 名玩家发放道具 (ITEM_ID={itemId})。", LogLevel.Message);
            }
            else
            {
                var found = room.Players.Find(p => p?.PublicInfo?.PlayerId == targetId);
                if (found == null)
                {
                    console.Log($"找不到 PlayerId={targetId} 的玩家。", LogLevel.Warning);
                    return;
                }
                SendHandItem(room, targetId, itemId);
                console.Log($"已给玩家 {found.Name}（#{targetId}）发放道具 (ITEM_ID={itemId})。", LogLevel.Message);
            }
        }

        // ── 工具方法 ─────────────────────────────────────────

        private static void SendHandItem(GameRoom room, int playerId, int itemId)
        {
            room.Broadcast(new S_MODIFY_PLAYER
            {
                PlayerId = playerId,
                Type     = EModifyPlayerEvent.ChangeHandItem,
                Value    = itemId
            });
        }

        private static bool TryParseItem(string s, out int itemId)
        {
            if (ItemAliases.TryGetValue(s, out itemId)) return true;
            return int.TryParse(s, out itemId);
        }
    }
}
