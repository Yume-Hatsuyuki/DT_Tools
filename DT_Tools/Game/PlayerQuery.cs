using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Game
{
    public static class PlayerQuery
    {
        public static List<Server.Game.Player> AliveTargets(Server.Game.GameRoom room)
            => room.Players
                .Where(p => p?.PublicInfo != null && p.IsAlive && !p.IsSpectator)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

        public static Server.Game.Player FindById(Server.Game.GameRoom room, int playerId)
            => room.Players.FirstOrDefault(p => p?.PublicInfo?.PlayerId == playerId);
    }
}
