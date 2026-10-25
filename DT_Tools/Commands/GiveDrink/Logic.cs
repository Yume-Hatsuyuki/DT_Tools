using Protocol;
using Server.Game;

namespace DT_Tools.Commands.GiveDrink
{
    internal static class GiveDrinkLogic
    {
        public static void SendHandItem(Server.Game.Player player, int itemId)
        {
            if (itemId <= 0)
            {
                player.RemoveHand(isForce: true);
                player.RemoveWeapon();
                return;
            }

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
