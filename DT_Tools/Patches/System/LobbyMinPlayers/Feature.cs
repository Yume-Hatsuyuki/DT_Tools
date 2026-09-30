using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.LobbyMinPlayers
{
    /// <summary>
    /// LOBBY_MIN_PLAYER：正式服返回配置值（原版 5）；Playtest 仍为 0。
    /// </summary>
    [PatchFeature(
        "房间开局最少人数：正式服可改（默认 5）；测试模式开启时仍为 0。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class LobbyMinPlayersFeature
    {
        [Config("正式服开局所需最少人数，游戏默认为 5。")]
        public static int Value = 5;
    }
}
