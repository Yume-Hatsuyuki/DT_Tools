using System;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Patches.System.LobbyMaxPlayers
{
    /// <summary>
    /// 房间人数上限：进房上限（HandleEnterPlayer 整替）、Steam Lobby 容器（CreateLobby 整替）、
    /// 开局出生点循环复用（GameStart 整替）三处联动。
    /// </summary>
    [PatchFeature(
        "房间人数上限：可修改开房最多人数。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class LobbyMaxPlayersFeature
    {
        [Config("游戏进房人数上限（1–16）。原版 8。", Min = 1, Max = 16)]
        public static int MaxMembersEntry = 8;

        [Config("Steam Lobby 容器上限（1–16）。若小于 MaxMembers 会抬到 MaxMembers。", Min = 1, Max = 16)]
        public static int SteamMemberLimitEntry = 16;

        internal static int MaxMembers => Math.Clamp(MaxMembersEntry, 1, 16);

        internal static int SteamMemberLimit => Math.Clamp(SteamMemberLimitEntry, 1, 16);

        internal static int EffectiveSteamMemberLimit =>
            Math.Clamp(Math.Max(SteamMemberLimit, MaxMembers), 1, 16);
    }
}
