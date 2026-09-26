using Server.Game;

namespace DT_Tools.Commands.GiveDrink
{
    /// <summary>/givedrink 输出格式化：可用道具列表 + 人类回复 + JSON 结果 DTO。</summary>
    internal static class GiveDrinkFormat
    {
        // 与 ItemAliases 对应的人类可读说明（每类取主别名，不重复列出短名）
        public const string ItemList =
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
            " 【手机/平板 - 原版扫描/开平板时手持】\n" +
            "  smaho/phone/tablet 手机            (4001)\n" +
            " 【灯笼】\n" +
            "  lantern          白灯笼            (4004)\n" +
            "  lantern_red      红灯笼            (4005)\n" +
            "  lantern_blue     蓝灯笼            (4006)\n" +
            " 【特殊】\n" +
            "  question_flower  问号花            (5001)\n" +
            "  0 / clear        清空手持物\n" +
            "  <数字>           直接指定 ITEM_ID\n" +
            "示例: /givedrink all can01   /givedrink #5 knife";

        public static string ReplyAll(int count, int itemId)
            => $"已给 {count} 名玩家发放道具 (ITEM_ID={itemId})。";

        public static string ReplySingle(Server.Game.Player target, int itemId)
            => $"已给玩家 {target.Name}（#{target.PublicInfo.PlayerId}）发放道具 (ITEM_ID={itemId})。";

        public static object ResultAll(int count, int itemId)
            => new { mode = "all", item = itemId, count };

        public static object ResultSingle(Server.Game.Player target, int itemId)
            => new { mode = "single", item = itemId, pid = target.PublicInfo.PlayerId, name = target.Name ?? "" };
    }
}
