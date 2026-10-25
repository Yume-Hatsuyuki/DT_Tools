using System;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.MastermindWeight
{
    [HarmonyPatch(typeof(Server.Game.GameRoom), nameof(Server.Game.GameRoom.SetMasterMind))]
    internal static class MastermindWeightSetMasterMindPatch
    {
        private static void Prefix(Server.Game.GameRoom __instance, ref Server.Game.Player player)
        {
            if (!Engine.Enabled<MastermindWeightFeature>())
                return;
            if (__instance == null || __instance.IsMigrating)
                return;

            var pool = __instance.Players
                .Where(p => p != null && !p.IsSpectator)
                .ToList();
            if (pool.Count == 0)
                return;

            int defaultWeight = MastermindWeightFeature.DefaultWeight;
            var weights = pool.Select(p => MastermindWeights.Get(p.AccountID, defaultWeight)).ToList();
            if (MastermindWeights.AllZero(weights))
                Log.Warn<MastermindWeightFeature>("所有玩家的格子数均为 0，黑幕回退为均匀抽取。");

            var chosen = MastermindWeights.Pick(pool, defaultWeight, () => UnityEngine.Random.value);
            if (chosen == null)
                return;

            foreach (var p in pool)
                p.Color = EPlayerColor.White;
            chosen.Color = EPlayerColor.Dark;
            player = chosen;

            float share = MastermindWeights.Share(weights, pool.IndexOf(chosen));
            Log.Info<MastermindWeightFeature>($"黑幕已按格子抽取：{chosen.Name}（本局概率约 {share:P0}）");
        }
    }
}
