using Protocol;

namespace DT_Tools.Game.WebTablet
{
    /// <summary>
    /// 头像键的推导规则。键对应 WebUI 内置的 SD 头像文件名。
    /// </summary>
    /// <remarks>
    /// Madeline（角色表 Name 为 Medelin）与 Mastermind 共用同一套资源：游戏中 Madeline 的 SD 图
    /// 由 Mastermind_SD.sprite 派生（见 Features/UnlockMadeline/Ui.cs InjectAll），
    /// 因此头像键统一为 "Mastermind"。
    /// 与 0.1.18c UI_ChatSubItem.cs:107（SetNameAndPortrait）及 UI_ClueSubItem.cs:218（FillText）的取图规则一致。
    /// </remarks>
    public static class WebTabletAvatar
    {
        /// <summary>
        /// 角色类型对应的头像键。Madeline 映射为 "Mastermind"，其余为枚举名。
        /// </summary>
        /// <param name="type">角色类型。</param>
        /// <returns>头像键。</returns>
        public static string OfCharacter(ECharacterType type)
            => type == ECharacterType.Madeline ? "Mastermind" : type.ToString();

        /// <summary>
        /// 玩家的头像键。找不到玩家或角色数据时使用 "Someone"（游戏内的无名剪影）。
        /// </summary>
        /// <param name="player">客户端玩家缓存，可为 null。</param>
        /// <returns>头像键。</returns>
        public static string OfPlayer(Player player)
            => player == null || player.CharData == null ? "Someone" : OfCharacter(player.CharData.Type);
    }
}
