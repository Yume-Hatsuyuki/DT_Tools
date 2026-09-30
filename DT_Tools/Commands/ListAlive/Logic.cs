using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.ListAlive
{
    /// <summary>/list_alive 业务：存活筛选（PlayerQuery）+ 阵营/SteamId 附加。</summary>
    internal static class ListAliveLogic
    {
        /// <summary>存活玩家条目（Host 端权威数据）。</summary>
        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public ulong SteamId;
            public EPlayerColor Color;
        }

        /// <summary>仅游戏中可用（选角结束之后）。</summary>
        public static bool IsInGame(EGameState state)
            => state != EGameState.Lobby
               && state != EGameState.NoneState
               && state != EGameState.PickCharacter;

        /// <summary>收集存活玩家（存活、非观战、非 Dummy），按 PlayerId 升序，附 Roster SteamId。</summary>
        public static List<Entry> Collect(GameRoom room)
        {
            return PlayerQuery.AliveTargets(room)
                .Select(p => new Entry
                {
                    Pid = p.PublicInfo.PlayerId,
                    Name = p.Name ?? "(未命名)",
                    SteamId = Managers.Player.GetRosterSteamId(p.PublicInfo.PlayerId),
                    Color = p.Color,
                })
                .ToList();
        }
    }
}
