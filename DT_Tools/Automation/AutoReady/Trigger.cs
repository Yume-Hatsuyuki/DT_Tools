using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Automation.AutoReady
{
    /// <summary>触发器：监测进入 / 离开大厅的时刻，维护阶段跟踪并驱动动作。</summary>
    internal static class AutoReadyTrigger
    {
        public static void Tick()
        {
            if (!GameState.TryGetState(out EGameState state))
                return;

            if (state != AutoReadyState.LastState)
            {
                if (state == EGameState.Lobby)
                {
                    AutoReadyState.BeginLobby();
                    Log.Info<AutoReadyModule>("已进入大厅，准备自动就绪");
                }
                else if (AutoReadyState.LastState == EGameState.Lobby)
                {
                    AutoReadyState.MarkLeft();
                }
                AutoReadyState.LastState = state;
            }

            if (state == EGameState.Lobby && !AutoReadyState.Done)
                AutoReadyAction.Run();
        }
    }
}
