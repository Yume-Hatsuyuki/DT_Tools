using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Fun.HasungBoost
{
    /// <summary>Hasung 冲刺能量累积与移速加成（服务端进程内共享状态）。</summary>
    internal static class HasungBoostLogic
    {
        /// <summary>各 Hasung 玩家（PlayerId）的当前冲刺能量。</summary>
        private static readonly Dictionary<int, int> _energy = new Dictionary<int, int>();

        /// <summary>已启动循环 tick 的玩家集合（防重复注册）。</summary>
        private static readonly HashSet<int> _ticked = new HashSet<int>();

        /// <summary>获取玩家当前能量（无记录视为 0）。</summary>
        public static int GetEnergy(int playerId)
            => _energy.TryGetValue(playerId, out int e) ? e : 0;

        /// <summary>每秒 tick：奔跑（消耗体力）累积能量，停跑衰减；结束后自动注册下一次。</summary>
        public static void Tick(Server.Game.Player owner)
        {
            // 循环：注册下一次 tick（玩家死亡/离开后自然停止）
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
                    owner.BuffComponent.RefreshSpeed(); // 触发 Postfix 应用最新加成
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<HasungBoostFeature>("Hasung 冲刺能量 tick 失败（可忽略）：" + ex.Message);
            }
        }

        /// <summary>确保该玩家已启动循环 tick（首次遇到时）。</summary>
        public static void EnsureTick(Server.Game.Player owner)
        {
            if (owner == null || owner.Data == null) return;
            int pid = owner.PublicInfo.PlayerId;
            if (_ticked.Add(pid))
                Server.Game.TimeManager.Instance.PushSurvivalJob(1, () => Tick(owner));
        }
    }

    /// <summary>
    /// 移速计算点（0.1.16b BuffComponent.RefreshSpeed，public）：Hasung 且持有点数时，
    /// 在计算结果上追加冲刺加成并广播最新移速。同时惰性启动每秒 tick。
    /// </summary>
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

                // 每点加成 = BoostPerPoint/100 档 SpeedDelta = (BoostPerPoint/100f)*560f 速度
                float extra = energy * (HasungBoostFeature.BoostPerPoint / 100f) * 560f;
                if (extra <= 0f)
                    return;
                owner.PrivateInfo.Speed = owner.PrivateInfo.Speed + extra;
                owner.SendChangeSpeed();
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<HasungBoostFeature>("Hasung 移速加成失败（可忽略）：" + ex.Message);
            }
        }
    }
}
