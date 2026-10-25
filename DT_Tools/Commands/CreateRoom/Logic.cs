using System.Collections;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Commands.CreateRoom
{
    internal static class CreateRoomLogic
    {
        private const float LobbyUiTimeoutSeconds = 15f;

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

            if (AccessTools.Field(typeof(UI_LobbyScene), "_section")
                    ?.GetValue(scene) is Define.ELobbySection section)
                Managers.Network.ReturnLobbySection = section;

            var voiceTask = AccessTools.Method(typeof(UI_LobbyScene), "PrepareVoiceForRoomAsync")
                ?.Invoke(scene, null) as System.Threading.Tasks.Task;
            if (voiceTask != null)
            {
                while (!voiceTask.IsCompleted)
                    yield return null;
            }
            else
            {
                Log.Warn("CreateRoom", "未找到 PrepareVoiceForRoomAsync，跳过语音预连。");
            }

            var scopeTask = Managers.Resource.LoadScopeAsync("School");
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
