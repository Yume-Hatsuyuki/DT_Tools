using System;
using System.Linq;

namespace DT_Tools.Game
{
    /// <summary>
    /// 随机道具模式（鱼池 / 货架等互斥选项统一用此枚举）。
    /// Normal = 含武器；Safe = 不含武器。
    /// </summary>
    public enum RandomItemMode
    {
        Normal = 0,
        Safe = 1,
    }

    /// <summary>
    /// 鱼池 / 货架共用道具池（Game 层游戏数据表；原 Patches/System/RandomItems 纯共享
    /// 目录上浮——武器区间判定与 60 项物品表属游戏行为数据，且消费方 ≥2 处）。
    /// 武器区间 [2000, 3000)。启用判定由调用方用 Engine.Enabled&lt;本功能&gt;() 自查。
    /// </summary>
    public static class ItemPools
    {
        public static readonly int[] Full =
        {
            Define.ITEM_ID_USB,
            Define.ITEM_ID_MANIKIN,
            Define.ITEM_ID_SURGERY_MANIKIN,
            Define.ITEM_ID_BATTERY_EMPTY,
            Define.ITEM_ID_BATTERY_FULL,
            Define.ITEM_ID_RED_FLOWER,
            Define.ITEM_ID_BLUE_FLOWER,
            Define.ITEM_ID_YELLOW_FLOWER,
            Define.ITEM_ID_PINK_FLOWER,
            Define.ITEM_ID_AMPLE,
            Define.ITEM_ID_MUSHROOM,
            Define.ITEM_ID_ICE_WATER,
            Define.ITEM_ID_ULTIMATEPOTION,
            Define.ITEM_ID_PICKAXE,
            Define.ITEM_ID_BLUEMINERAL,
            Define.ITEM_ID_GREENMINERAL,
            Define.ITEM_ID_REDMINERAL,
            Define.ITEM_ID_ESSENCE,
            Define.ITEM_ID_SHAKER_BALL_01,
            Define.ITEM_ID_SHAKER_BALL_02,
            Define.ITEM_ID_SYRINGE_EMPTY,
            Define.ITEM_ID_SYRINGE_RED,
            Define.ITEM_ID_SYRINGE_GREEN,
            Define.ITEM_ID_SYRINGE_BLUE,
            Define.ITEM_ID_SYRINGE_YELLOW,
            Define.ITEM_ID_POTION_RED,
            Define.ITEM_ID_POTION_GREEN,
            Define.ITEM_ID_POTION_BLUE,
            Define.ITEM_ID_POTION_YELLOW,
            Define.ITEM_ID_RED_BOOK,
            Define.ITEM_ID_BLUE_BOOK,
            Define.ITEM_ID_GREEN_BOOK,
            Define.ITEM_ID_YELLOW_BOOK,
            Define.ITEM_ID_FISHING_ROD,
            Define.ITEM_ID_FISH_NORMAL,
            Define.ITEM_ID_FISH_RARE,
            Define.ITEM_ID_FISH_GOLD,
            Define.ITEM_ID_TROPHY_GOLD,
            Define.ITEM_ID_TROPHY_SILVER,
            Define.ITEM_ID_TROPHY_BRONZE,
            Define.ITEM_ID_KNIFE,
            Define.ITEM_ID_BAT,
            Define.ITEM_ID_HAMMER,
            Define.ITEM_ID_SHOVEL,
            Define.ITEM_ID_ACCORDION,
            Define.ITEM_ID_CAN01,
            Define.ITEM_ID_CAN02,
            Define.ITEM_ID_CAN03,
            Define.ITEM_ID_CAN04,
            Define.ITEM_ID_CAN05,
            Define.ITEM_ID_ADRENALINE,
            Define.ITEM_ID_TOYHAMMER,
            Define.ITEM_ID_AIRHORN,
            Define.ITEM_ID_BELL,
            Define.ITEM_ID_SYRINGE_POTION,
            Define.ITEM_SMAHO,
            Define.ITEM_ID_LANTERN,
            Define.ITEM_ID_LANTERN_RED,
            Define.ITEM_ID_LANTERN_BLUE,
            Define.ITEM_ID_QUESTION_FLOWER,
        };

        public static readonly int[] Safe = Full.Where(id => !IsWeapon(id)).ToArray();

        public static bool IsWeapon(int itemId) => itemId >= 2000 && itemId < 3000;

        public static int[] ForMode(RandomItemMode mode) =>
            mode == RandomItemMode.Safe ? Safe : Full;

        /// <summary>
        /// 货架扩展池提供者：由货架随机道具功能（SupplyShelf）装载时发布，取值含
        /// 功能开启判定（未开启返回 null）。下游（SupplyShelfRefill 补货池）只读本层，
        /// 消除 Patches 功能间的横向依赖（AGENTS.md §4）。
        /// </summary>
        public static Func<int[]> ShelfPoolProvider;
    }
}
