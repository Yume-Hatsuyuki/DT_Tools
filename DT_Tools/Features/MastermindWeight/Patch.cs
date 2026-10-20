using System;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.MastermindWeight
{
    /// <summary>
    /// 替换黑幕的抽取结果：按权重从非观战玩家中重新抽取，并同步颜色。
    /// </summary>
    /// <remarks>
    /// 原版：GameRoom.StartPick 先把全员设为白、随机选一人设为黑（0.1.18c Server.Game/GameRoom.cs:1608-1612），
    /// 再调用 SetMasterMind。这里在 SetMasterMind 的 Prefix 中改写 player 参数，并把颜色统一按新结果重设。
    /// 池子与 SpectatorJoin 的替换版一致（非观战者，Features/SpectatorJoin/Feature.GameStart.cs:184）。
    /// IsMigrating 为真时是迁移恢复黑幕身份（MigrationOrchestrator.cs:186、196），不得重抽。
    /// </remarks>
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
