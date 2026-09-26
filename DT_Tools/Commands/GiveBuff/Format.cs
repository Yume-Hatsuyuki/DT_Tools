using Protocol;

namespace DT_Tools.Commands.GiveBuff
{
    /// <summary>/givebuff 输出格式化：可用 BUFF 列表 + 人类回复 + JSON 结果 DTO。</summary>
    internal static class GiveBuffFormat
    {
        public const string BuffList =
            "━━━ 可用 BUFF（对照 Protocol.EBuffType 全量收录） ━━━\n" +
            " 【移动/控制】\n" +
            "  slow             减速（原版：卡门/被玩具武器击中触发）\n" +
            "  stun             眩晕，锁定操作\n" +
            "  stop             定身，完全锁定操作\n" +
            "  exhausted        体力耗尽，减速\n" +
            " 【道具效果】\n" +
            "  potioncorrect    正确注射，加速\n" +
            "  potionwrong      错误注射，异常状态\n" +
            " 【角色技能（隐藏，原版不广播）】\n" +
            "  staminaup        体力提升（Hasung 技能 Raasrush）\n" +
            "  scanup           扫描加速（Miyuki 技能 DetailCheck，读条 2s→0.5s）\n" +
            "  quantumleap      量子跃迁（Jeremy 技能 MindControl）\n" +
            "  deaddetective    死亡广播（Louis 技能 Telekinesis）\n" +
            " 【场景/系统】\n" +
            "  theworld         时间停止表现（Seol 技能 TimeStop，客户端表现完整）\n" +
            "  footprint        留下可追踪脚印\n" +
            " 【绝望值系统（服务端未见触发逻辑，实装状态未知）】\n" +
            "  despair          绝望状态\n" +
            "  despairimmune    绝望免疫\n" +
            "  despairguard     绝望守护\n" +
            "  grouppanelty     群体惩罚（RefreshGroupPanelty 当前为空实现）\n" +
            "【清除】\n" +
            "  /givebuff all clear\n" +
            "  /givebuff #1 clear\n" +
            "  /givebuff #1 footprint 0\n" +
            "  /givebuff #1 footprint clear\n" +
            "【添加示例】\n" +
            "  /givebuff all scanup              ← 省略秒数，默认 3600 秒\n" +
            "  /givebuff all scanup 3600         ← 3600 秒为上限，超出会被钳制\n" +
            "  /givebuff #5 slow 5";

        public static string ReplyClearAll(bool all, int count)
            => all ? $"已清除 {count} 名玩家的全部 BUFF。" : "已清除玩家全部 BUFF。";

        public static string ReplyClearOne(bool all, int count, EBuffType buff)
            => all ? $"已从 {count} 名玩家清除 BUFF={buff}。" : $"已清除 BUFF={buff}。";

        public static string ReplyAdd(bool all, int count, EBuffType buff, int seconds)
            => all ? $"已给 {count} 名玩家添加 BUFF={buff}，持续 {seconds} 秒。"
                   : $"已添加 BUFF={buff}，持续 {seconds} 秒。";

        public static object ResultClearAll(bool all, int count)
            => new { mode = all ? "all" : "single", action = "clear_all", count };

        public static object ResultClearOne(bool all, int count, EBuffType buff)
            => new { mode = all ? "all" : "single", action = "clear_one", buff = buff.ToString(), count };

        public static object ResultAdd(bool all, int count, EBuffType buff, int seconds)
            => new
            {
                mode = all ? "all" : "single",
                action = "add",
                buff = buff.ToString(),
                seconds,
                count,
            };
    }
}
