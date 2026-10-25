using Protocol;

namespace DT_Tools.Game.WebTablet
{
    public static class WebTabletAvatar
    {
        public static string OfCharacter(ECharacterType type)
            => type == ECharacterType.Madeline ? "Mastermind" : type.ToString();

        public static string OfPlayer(Player player)
            => player == null || player.CharData == null ? "Someone" : OfCharacter(player.CharData.Type);
    }
}
