using System.Collections;
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace DT_Tools.Commands.Join
{
    /// <summary>
    /// /join 业务编排：离开当前房间（原版退出确认同路径）→ 等大厅 UI 就绪 →
    /// 按房间码搜索 → 复用原版进房编排 ProceedLobbyEnter（加载界面/语音/资源域）。
    /// 目标房间正在对局时，原版本就以观战加入（UI_LobbyScene 的确认弹窗只是提示，
    ///ProceedSteamJoin 两条分支同路径，UI_LobbyScene.cs:1146-1153），此处直接进入。
    /// </summary>
    internal static class JoinLogic
    {
        private const float LobbyUiTimeoutSeconds = 15f;

        /// <summary>是否在房内：NetworkManager.InRoom 为 private（:165，GameServer != null），
        /// GameServer 是公开读（NetworkManager.cs:130）。</summary>
        public static bool IsInRoom => Managers.Network?.GameServer != null;

        /// <summary>离开当前房间：与原版退出确认 OnClickExitYes 同路径（UI_GameScene.cs:2302-2308）。</summary>
        public static void LeaveCurrentRoom()
        {
            Managers.Network.Leave();                              // 0.1.15b NetworkManager.cs:1305
            Managers.Scene.LoadScene(Define.EScene.LobbyScene);    // 0.1.15b SceneManagerEx.cs:9
        }

        /// <summary>启动加入流程（协程等大厅 UI，随后异步搜索房间码）。</summary>
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

        /// <summary>
        /// 按码搜索并进入：FindLobbyByCode → 回调在主线程（游戏主循环泵 Steam 回调）；
        /// 进房复用原版 ProceedLobbyEnter（私有方法，字符串定位：0.1.15b UI_LobbyScene.cs:1101），
        /// 加载界面、语音预连接、School 资源域装载全部按原版编排执行。
        /// </summary>
        private static void JoinByCode(UI_LobbyScene scene, string code)
        {
            Managers.Network.FindLobbyByCode(code, lobbyId =>   // 0.1.15b NetworkManager.cs:254
            {
                if (lobbyId == CSteamID.Nil)
                {
                    Log.Warn("Join", $"未找到房间码 {code} 对应的房间（注意：码随房间解散失效）。");
                    return;
                }
                if (SteamLobbyManager.IsLobbyInGame(lobbyId))    // 0.1.15b SteamLobbyManager.cs:471
                    Log.Info("Join", $"房间 {code} 正在对局，将以观战方式加入。");
                AccessTools.Method(typeof(UI_LobbyScene), "ProceedLobbyEnter")
                    ?.Invoke(scene, new object[] { lobbyId });
                Log.Info("Join", $"已发起对房间 {code} 的加入。");
            });
        }
    }
}
