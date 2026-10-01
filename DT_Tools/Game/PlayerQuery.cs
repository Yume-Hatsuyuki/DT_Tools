using System.Collections.Generic;
using System.Linq;

namespace DT_Tools.Game
{
    /// <summary>房主端权威玩家表查询（Server.Game.GameRoom.Players）。</summary>
    public static class PlayerQuery
    {
        /// <summary>
        /// 存活且非观战的玩家，按 PlayerId 升序。
        /// 假人（IsDummy）是服务端完整实体（只是没有客户端回发包），服务端测试命令
        /// （处决/发放/保护/列表）需要把它们当作正常目标，不再排除。
        /// </summary>
        public static List<Server.Game.Player> AliveTargets(Server.Game.GameRoom room)
            => room.Players
                .Where(p => p?.PublicInfo != null && p.IsAlive && !p.IsSpectator)
                .OrderBy(p => p.PublicInfo.PlayerId)
                .ToList();

        public static Server.Game.Player FindById(Server.Game.GameRoom room, int playerId)
            => room.Players.FirstOrDefault(p => p?.PublicInfo?.PlayerId == playerId);
    }
}
