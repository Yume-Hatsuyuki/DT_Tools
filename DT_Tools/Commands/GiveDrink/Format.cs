using System.Text;
using Server.Game;

namespace DT_Tools.Commands.GiveDrink
{
    /// <summary>/givedrink 输出格式化：可用道具列表 + 人类回复 + JSON 结果 DTO。</summary>
    internal static class GiveDrinkFormat
    {
        /// <summary>
        /// 可用道具列表：由 Args.ItemDefs（别名解析的同一数据源）生成，
        /// 消除旧版文案表与别名表的双份维护口径漂移。
        /// </summary>
        public static string ItemList
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine("━━━ 可用道具（对照 Define.cs 全量收录） ━━━");
                string lastGroup = null;
                foreach (var def in GiveDrinkArgs.ItemDefs)
                {
                    if (def.Group != lastGroup)
                    {
                        sb.AppendLine($" 【{def.Group}】");
                        lastGroup = def.Group;
                    }
                    sb.AppendLine($"  {def.Aliases[0],-16} {def.Label}  ({def.Id})");
                }
                sb.AppendLine(" 【清空】");
                sb.AppendLine("  0 / clear         清空手持物");
                sb.AppendLine("  <数字>            直接指定 ITEM_ID");
                sb.Append("示例: /givedrink all can01   /givedrink #5 knife");
                return sb.ToString();
            }
        }

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
