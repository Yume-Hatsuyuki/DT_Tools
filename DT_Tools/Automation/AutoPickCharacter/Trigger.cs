using DT_Tools.Game;
using Protocol;

namespace DT_Tools.Automation.AutoPickCharacter
{
    /// <summary>触发器：监测进入 / 离开选角阶段与大厅的时刻，维护阶段跟踪并驱动动作。</summary>
    internal static class AutoPickCharacterTrigger
    {
        public static void Tick()
        {
            if (!GameState.TryGetState(out EGameState state))
                return;

            if (state != AutoPickCharacterState.LastState)
            {
                if (state == EGameState.PickCharacter)
                {
                    AutoPickCharacterState.BeginPickPhase();
                    Log.Info<AutoPickCharacterModule>("已进入选角阶段");
                }
                else if (AutoPickCharacterState.LastState == EGameState.PickCharacter)
                {
                    Log.Info<AutoPickCharacterModule>(
                        $"离开选角阶段 → {state}（本阶段尝试 {AutoPickCharacterState.Attempts} 次）");
                    AutoPickCharacterState.ResetPhase();
                }

                if (state == EGameState.Lobby)
                {
                    AutoPickCharacterState.EnterLobby();
                    Log.Info<AutoPickCharacterModule>("已进入大厅");
                }

                AutoPickCharacterState.LastState = state;
            }

            if (state == EGameState.Lobby && AutoPickCharacterModule.SyncLobby)
                AutoPickCharacterAction.SyncLobbyCharacter();

            if (state == EGameState.PickCharacter)
                AutoPickCharacterAction.AutoPick();
        }
    }
}
