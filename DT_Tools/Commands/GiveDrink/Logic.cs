using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GiveDrink
{
    /// <summary>/givedrink 业务：手持道具发放（走原生 ItemManager 链路）。</summary>
    internal static class GiveDrinkLogic
    {
        /// <summary>
        /// itemId &lt;= 0 → 清空手持物；武器发放前先移除已有武器，避免 InsertWeapon
        /// 内 Assert 失败；其余走原生发放链路：创建真实 Item → 写入 Hand/Weapon →
        /// 发 S_ADD_ITEM → 同步
        /// （0.1.16b Server.Game/Player.cs:1626 RemoveHand / :1643 RemoveWeapon / :150 Weapon，
        ///  Server.Game/ItemManager.cs:17 CreateAndInsertInven）。
        /// 调用前提：命令入口已校验 DataId 存在于 Managers.Data.ItemDic（否则 Item 构造
        /// 得到 Data=null 的脏 Item，见 ItemManager.cs:17-22 先 Add 后解引用的顺序）。
        ///
        /// 假人补差：InsertWeapon/InsertInven 只写服务端槽位，"手持显示"靠客户端收到
        /// S_ADD_ITEM 后回发 C_MODIFY_PLAYER(ChangeHandItem) 才落 PublicInfo.HandItemId
        /// 并广播（真人链路）。假人没有客户端——补一次服务端 HandItemObjectId 赋值
        /// （setter 会写 PublicInfo.HandItemId 并广播，Player.cs:374-402），否则其他客户端
        /// 看不到假人手持道具、攻击判定（AttackPlayer 读 PublicInfo.HandItemId，
        /// GameRoom.cs:2396）也取不到道具数据。清空路径无需补：RemoveHand/RemoveWeapon
        /// 内部走 HandItemObjectId=-1（Player.cs:1631/:1649），setter 同样广播。
        /// </summary>
        public static void SendHandItem(Server.Game.Player player, int itemId)
        {
            if (itemId <= 0)
            {
                player.RemoveHand(isForce: true);
                player.RemoveWeapon();
                return;
            }

            // 武器判定与原版同源：ItemManager.CreateAndInsertInven 按 item.Data.Type 分流
            // （0.1.16b ItemManager.cs:21-26），不再用 2xxx 区间猜测
            if (IsWeapon(itemId) && player.Weapon != null)
                player.RemoveWeapon();

            var item = ItemManager.Instance.CreateAndInsertInven(player, itemId);
            if (player.IsDummy && item != null)
                player.HandItemObjectId = item.Info.ObjectId;
        }

        private static bool IsWeapon(int itemId)
            => Managers.Data?.ItemDic != null
               && Managers.Data.ItemDic.TryGetValue(itemId, out Data.ItemData data)
               && data.Type == EItemType.Weapon;
    }
}
