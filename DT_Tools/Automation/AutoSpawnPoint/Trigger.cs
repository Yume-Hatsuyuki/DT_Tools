using DT_Tools.Core;
using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Automation.AutoSpawnPoint
{
    /// <summary>触发器：监测进入 Survive 的时刻，维护阶段跟踪并驱动动作。</summary>
    internal static class AutoSpawnPointTrigger
    {
        public static void Tick()
        {
            if (!GameState.TryGetState(out EGameState state))
                return;

            if (state != AutoSpawnPointState.LastState)
            {
                if (state == EGameState.Survive)
                {
                    AutoSpawnPointState.BeginRound();
                    Log.Info<AutoSpawnPointModule>("已进入 Survive，准备传送");
                }
                else if (AutoSpawnPointState.LastState == EGameState.Survive)
                {
                    AutoSpawnPointState.MarkLeft();
                    Log.Info<AutoSpawnPointModule>($"离开 Survive → {state}");
                }
                AutoSpawnPointState.LastState = state;
            }

            if (state == EGameState.Survive && !AutoSpawnPointState.DoneThisRound)
                AutoSpawnPointAction.Run();
        }
    }
}
