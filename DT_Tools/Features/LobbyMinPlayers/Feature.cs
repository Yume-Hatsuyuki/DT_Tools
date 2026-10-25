using DT_Tools.Core.Attributes;

namespace DT_Tools.Features.LobbyMinPlayers
{
    [PatchFeature(
        "房间开局最少人数：正式服可改（默认 5）；测试模式开启时仍为 0。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.SysRoom | FeatureTag.GameRound,
        Author = "梦初雪")]
    public sealed class LobbyMinPlayersFeature
    {
        [Config("正式服开局所需最少人数，游戏默认为 5。")]
        public static int Value = 5;
    }
}
