// 作用：判定自动取武器的自动化触发条件；不负责动作执行与配置声明。
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Automation.AutoAcquireWeapon
{
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
