using System.Collections;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.CreateRoom
{
    /// <summary>
    /// /create_room 业务编排：等大厅界面就绪 → 复刻原版 OnClickCreateButton 的
    /// 建房前序（0.1.16b UI_LobbyScene.cs:1198）：回跳段 → 语音预连 → School 资源域
    /// → Steam 建房。mic/语言/公开-私密的缺省值读大厅 UI 私有字段（与原版同源），
    /// 显式参数覆盖。回调在主线程（游戏主循环泵 Steam 回调）。
    /// </summary>
    internal static class CreateRoomLogic
    {
        private const float LobbyUiTimeoutSeconds = 15f;

        /// <summary>启动建房流程（协程等大厅 UI，随后按原版次序异步推进）。</summary>
        public static void StartCreateFlow(string roomName, string privacy)
        {
            CoroutineHost.Start(CoCreate(roomName, privacy));
        }

        private static IEnumerator CoCreate(string roomName, string privacy)
        {
            float deadline = Time.realtimeSinceStartup + LobbyUiTimeoutSeconds;
            UI_LobbyScene scene = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                scene = Managers.UI?.SceneUI as UI_LobbyScene;
                if (scene != null)
                    break;
                yield return new WaitForSeconds(0.25f);
            }
            if (scene == null)
            {
                Log.Warn("CreateRoom", "等待大厅界面超时，已放弃建房。请回到主界面后重试 /create_room。");
                yield break;
            }
            if (RoomFlow.IsInRoom)
            {
                Log.Warn("CreateRoom", "等待期间已进入房间，取消建房。");
                yield break;
            }

            // 原版次序：回跳段（退出房间后回大厅的落点）→ 语音预连 → 资源域 → 建房
            if (AccessTools.Field(typeof(UI_LobbyScene), "_section")
                    ?.GetValue(scene) is Define.ELobbySection section)
                Managers.Network.ReturnLobbySection = section;

            var voiceTask = AccessTools.Method(typeof(UI_LobbyScene), "PrepareVoiceForRoomAsync")
                ?.Invoke(scene, null) as System.Threading.Tasks.Task;   // 0.1.16b UI_LobbyScene.cs:1557
            if (voiceTask != null)
            {
                while (!voiceTask.IsCompleted)
                    yield return null;
            }
            else
            {
                Log.Warn("CreateRoom", "未找到 PrepareVoiceForRoomAsync，跳过语音预连。");
            }

            var scopeTask = Managers.Resource.LoadScopeAsync("School");   // 0.1.16b ResourceManager.cs:220
            while (scopeTask != null && !scopeTask.IsCompleted)
                yield return null;
            Managers.Data.LoadMapSet(EMapType.School);

            string uiPrivacy = AccessTools.Field(typeof(UI_LobbyScene), "_roomPublic")?.GetValue(scene) as string ?? "public";
            bool isPrivate = (privacy ?? uiPrivacy).Equals("private", System.StringComparison.OrdinalIgnoreCase);
            string mic = AccessTools.Field(typeof(UI_LobbyScene), "_roomMic")?.GetValue(scene) as string ?? "on";
            string lang = AccessTools.Field(typeof(UI_LobbyScene), "_roomLanguage")?.GetValue(scene) as string ?? "ANY";

            Log.Info("CreateRoom", $"已发起建房：「{roomName}」（{(isPrivate ? "私密" : "公开")}，麦克风 {mic}，语言 {lang}）…");
            Managers.Network.CreateLobby(isPrivate, mic, lang, roomName, success =>
            {
                if (success)
                    Log.Info("CreateRoom", $"房间已创建 → 房间码 {Managers.Network.RoomCode}（/room_code 可随时查看）。本机为房主。");
                else
                    Log.Warn("CreateRoom", "建房失败（Steam lobby 创建未成功），详见 BepInEx 主日志。");
            });
        }
    }
}
