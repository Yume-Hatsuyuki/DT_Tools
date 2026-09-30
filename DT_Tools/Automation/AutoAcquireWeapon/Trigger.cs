using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Automation.AutoAcquireWeapon
{
    /// <summary>触发器：监测进入 / 离开 Survive 的时刻，维护阶段跟踪并驱动动作。</summary>
    internal static class AutoAcquireWeaponTrigger
    {
        public static void Tick()
        {
            if (!GameState.TryGetState(out EGameState state))
                return;

            if (state != AutoAcquireWeaponState.LastState)
            {
                if (state == EGameState.Survive)
                {
                    AutoAcquireWeaponState.BeginRound();
                    Log.Info<AutoAcquireWeaponModule>("已进入 Survive，准备自动取刀");
                }
                else if (AutoAcquireWeaponState.LastState == EGameState.Survive)
                {
                    AutoAcquireWeaponState.MarkLeft();
                    Log.Info<AutoAcquireWeaponModule>($"离开 Survive → {state}");
                }
                AutoAcquireWeaponState.LastState = state;
            }

            if (state == EGameState.Survive && !AutoAcquireWeaponState.DoneThisRound)
                AutoAcquireWeaponAction.Run();
        }
    }
}
