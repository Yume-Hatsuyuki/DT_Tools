using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.ListAlive
{
    /// /list_alive 业务：存活筛选（PlayerQuery）+ 阵营/SteamId 附加。
    internal static class ListAliveLogic
    {
        /// 存活玩家条目（Host 端权威数据）。
        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public ulong SteamId;
            public EPlayerColor Color;
        }

        /// 仅游戏中可用（选角结束之后；判定上浮 Game/GameState.IsInGame）。
        public static bool IsInGame(EGameState state) => global::DT_Tools.Game.GameState.IsInGame(state);

        /// 收集存活玩家（存活、非观战、非 Dummy），按 PlayerId 升序，附 Roster SteamId。
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
