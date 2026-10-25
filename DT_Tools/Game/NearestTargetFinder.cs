using System;
using Protocol;
using UnityEngine;

namespace DT_Tools.Game
{
    public static class NearestTargetFinder
    {
        private const int SightBlockMask = 12288;

        public static Player Find(Vector2 from, float range, Predicate<Player> filter = null)
        {
            Player best = null;
            float bestDist = float.PositiveInfinity;

            foreach (Player player in Managers.Player.Players.Values)
            {
                if (player.State == EPlayerState.Hide)
                    continue;
                if (filter != null && !filter(player))
                    continue;

                Vector2 direction = player.Position - from;
                float magnitude = direction.magnitude;
                if (magnitude > range)
                    continue;

                if (!Physics2D.Raycast(from, direction, magnitude, SightBlockMask) &&
                    magnitude < bestDist)
                {
                    best = player;
                    bestDist = magnitude;
                }
            }

            return best;
        }
    }
}
