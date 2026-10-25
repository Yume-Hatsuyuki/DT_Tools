using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.HasungBoost
{
    internal static class HasungBoostLogic
    {
        private static readonly Dictionary<int, int> _energy = new Dictionary<int, int>();

        private static readonly HashSet<int> _ticked = new HashSet<int>();

        public static int GetEnergy(int playerId)
            => _energy.TryGetValue(playerId, out int e) ? e : 0;

        public static void Tick(Server.Game.Player owner)
        {
            Server.Game.TimeManager.Instance.PushSurvivalJob(1, () => Tick(owner));

            try
            {
                if (owner == null || !owner.IsAlive || owner.Data == null
                    || owner.Data.Type != ECharacterType.Hasung)
                {
                    _energy.Remove(owner?.PublicInfo.PlayerId ?? -1);
                    return;
                }

                int pid = owner.PublicInfo.PlayerId;
                _energy.TryGetValue(pid, out int cur);
                bool running = owner.State == EPlayerState.Run
                    && !owner.BuffComponent.HasBuff(EBuffType.Exhausted);
                if (running)
                {
                    if (cur < HasungBoostFeature.MaxBoostPoints)
                        cur++;
                }
                else if (cur > 0)
                {
                    cur -= HasungBoostFeature.DecayPerSec;
                    if (cur < 0) cur = 0;
                }
                _energy[pid] = cur;
                if (cur > 0)
                    owner.BuffComponent.RefreshSpeed();
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<HasungBoostFeature>(ex, "Hasung 冲刺能量 tick 失败（可忽略）");
            }
        }

        public static void EnsureTick(Server.Game.Player owner)
        {
            if (owner == null || owner.Data == null) return;
            int pid = owner.PublicInfo.PlayerId;
            if (_ticked.Add(pid))
                Server.Game.TimeManager.Instance.PushSurvivalJob(1, () => Tick(owner));
        }
    }

    [HarmonyPatch(typeof(Server.Game.BuffComponent), "RefreshSpeed")]
    internal static class HasungBoostSpeedPatch
    {
        private static void Postfix(Server.Game.BuffComponent __instance)
        {
            try
            {
                if (!Engine.Enabled<HasungBoostFeature>())
                    return;
                var owner = __instance?.Owner;
                if (owner == null || owner.Data == null) return;

                HasungBoostLogic.EnsureTick(owner);

                if (owner.Data.Type != ECharacterType.Hasung)
                    return;
                int energy = HasungBoostLogic.GetEnergy(owner.PublicInfo.PlayerId);
                if (energy <= 0)
                    return;

                float extra = energy * (HasungBoostFeature.BoostPerPoint / 100f) * 560f;
                if (extra <= 0f)
                    return;
                owner.PrivateInfo.Speed = owner.PrivateInfo.Speed + extra;
                owner.SendChangeSpeed();
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<HasungBoostFeature>(ex, "Hasung 移速加成失败（可忽略）");
            }
        }
    }
}
