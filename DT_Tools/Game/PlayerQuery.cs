using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Game
{
    /// <summary>房主端权威玩家表查询（Server.Game.GameRoom.Players）。</summary>
    public static class PlayerQuery
    {
        /// <summary>存活且非观战、非 Dummy（主机占位）的玩家，按 PlayerId 升序。</summary>
        public static List<Server.Game.Player> AliveTargets(Server.Game.GameRoom room)
            => room.Players
                .Where(p => p?.PublicInfo != null && p.IsAlive && !p.IsSpectator && !p.IsDummy)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

        public static Server.Game.Player FindById(Server.Game.GameRoom room, int playerId)
            => room.Players.FirstOrDefault(p => p?.PublicInfo?.PlayerId == playerId);
    }
}
