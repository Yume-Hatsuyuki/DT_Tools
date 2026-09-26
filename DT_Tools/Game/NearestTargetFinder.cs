using System;
using Protocol;
using UnityEngine;

namespace DT_Tools.Game
{
    /// <summary>
    /// 最近目标查找：0.1.15b MyPlayer.GetTargetPlayer（:2162，黑方攻击）与
    /// GetHandWeaponTarget（:2191，递刀）的共用骨架——
    /// 遍历 Managers.Player.Players（0.1.15b PlayerManager.cs:42），
    /// 跳过 EPlayerState.Hide，Physics2D.Raycast 视线遮挡
    /// （层掩码 12288：0.1.15b MyPlayer.cs:2180 / :2205），取距离最小者。
    /// 调用方经谓词附加各自的过滤条件（如递刀排除 KnownBlackIds）。
    /// </summary>
    public static class NearestTargetFinder
    {
        /// <summary>视线遮挡层掩码（0.1.15b MyPlayer.cs:2180，攻击/递刀共用同一常量）。</summary>
        private const int SightBlockMask = 12288;

        /// <summary>
        /// 从 from 出发、range 距离内、未被地形遮挡的最近玩家；谓词返回 false 的跳过。
        /// 复刻原版语义：Raycast 命中遮挡层（返回 true）视为不可直达，剔除该目标。
        /// </summary>
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
