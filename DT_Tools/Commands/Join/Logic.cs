using System.Collections;
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace DT_Tools.Commands.Join
{
    internal static class JoinLogic
    {
        private const float LobbyUiTimeoutSeconds = 15f;

        public static void StartJoinFlow(string code)
        {
            CoroutineHost.Start(CoJoin(code));
        }

        private static IEnumerator CoJoin(string code)
        {
            float deadline = Time.realtimeSinceStartup + LobbyUiTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (Managers.UI?.SceneUI is UI_LobbyScene scene)
                {
                    JoinByCode(scene, code);
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
            }
            Log.Warn("Join", "等待大厅界面超时，已放弃加入。请回到主界面后重试 /join。");
        }

        private static void JoinByCode(UI_LobbyScene scene, string code)
        {
            Managers.Network.FindLobbyByCode(code, lobbyId =>
            {
                if (lobbyId == CSteamID.Nil)
                {
                    Log.Warn("Join", $"未找到房间码 {code} 对应的房间（注意：码随房间解散失效）。");
                    return;
                }
                if (SteamLobbyManager.IsLobbyInGame(lobbyId))
                    Log.Info("Join", $"房间 {code} 正在对局，将以观战方式加入。");
                AccessTools.Method(typeof(UI_LobbyScene), "ProceedLobbyEnter")
                    ?.Invoke(scene, new object[] { lobbyId });
                Log.Info("Join", $"已发起对房间 {code} 的加入。");
            });
        }
    }
}
