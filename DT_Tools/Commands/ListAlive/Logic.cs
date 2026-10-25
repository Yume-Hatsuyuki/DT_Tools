using System.Collections.Generic;
using System.Linq;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.ListAlive
{
    internal static class ListAliveLogic
    {
        public sealed class Entry
        {
            public int Pid;
            public string Name;
            public ulong SteamId;
            public EPlayerColor Color;
        }

        public static bool IsInGame(EGameState state) => global::DT_Tools.Game.GameState.IsInGame(state);

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
