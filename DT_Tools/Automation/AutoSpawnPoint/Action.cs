using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    /// <summary>动作：按配置解析目标点并执行本机瞬移（延迟等待、状态拦截、失败后隔一个延迟周期重试）。</summary>
    internal static class AutoSpawnPointAction
    {
        public static void Run()
        {
            float delay = Mathf.Max(0f, AutoSpawnPointModule.DelaySeconds);
            if (AutoSpawnPointState.PhaseEnterRealtime < 0f)
                AutoSpawnPointState.PhaseEnterRealtime = Time.realtimeSinceStartup;

            if (Time.realtimeSinceStartup - AutoSpawnPointState.PhaseEnterRealtime < delay)
            {
                if (!AutoSpawnPointState.WaitingLogged)
                {
                    Log.Info<AutoSpawnPointModule>($"等待 {delay:0.##}s 后传送");
                    AutoSpawnPointState.WaitingLogged = true;
                }
                return;
            }

            var my = Managers.Player?.MyPlayer;
            if (my == null || my.PrivateInfo == null)
                return;

            if (my.State == EPlayerState.Hide || my.State == EPlayerState.Sit)
            {
                Log.Warn<AutoSpawnPointModule>($"状态 {my.State} 无法传送，本局跳过");
                AutoSpawnPointState.MarkLeft();
                return;
            }

            if (!AutoSpawnPointLogic.TryResolveTarget(out PosInfo target, out string label))
            {
                AutoSpawnPointState.MarkLeft();
                return;
            }

            if (!Teleport.TryTeleport(my, target, out string error))
            {
                Log.Warn<AutoSpawnPointModule>($"传送失败: {error}");
                AutoSpawnPointState.PhaseEnterRealtime = Time.realtimeSinceStartup;
                return;
            }

            AutoSpawnPointState.MarkLeft();
            Log.Info<AutoSpawnPointModule>($"已传送到 {label} → ({target.X:F0}, {target.Y:F0})");
        }
    }
}
