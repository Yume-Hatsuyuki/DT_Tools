using System;
using DT_Tools.Game;
using Protocol;
using UnityEngine;

namespace DT_Tools.Automation.AutoPickCharacter
{
    internal static class AutoPickCharacterAction
    {
        public static void AutoPick()
        {
            float delay = Mathf.Max(0f, AutoPickCharacterModule.DelaySeconds);
            if (AutoPickCharacterState.PhaseEnterRealtime < 0f)
                AutoPickCharacterState.PhaseEnterRealtime = Time.realtimeSinceStartup;

            if (Time.realtimeSinceStartup - AutoPickCharacterState.PhaseEnterRealtime < delay)
            {
                if (!AutoPickCharacterState.WaitingLogged)
                {
                    Log.Info<AutoPickCharacterModule>($"等待 {delay:0.##}s 后开始发包（等服务端选角就绪）");
                    AutoPickCharacterState.WaitingLogged = true;
                }
                return;
            }

            int maxAttempts = Mathf.Max(1, AutoPickCharacterModule.MaxAttempts);
            if (AutoPickCharacterState.Attempts >= maxAttempts)
            {
                if (!AutoPickCharacterState.GaveUpLogged)
                {
                    Log.Warn<AutoPickCharacterModule>($"已达最大尝试次数 {maxAttempts}，停止本阶段发包");
                    AutoPickCharacterState.GaveUpLogged = true;
                }
                return;
            }

            float interval = Mathf.Max(0.1f, AutoPickCharacterModule.RetryInterval);
            if (AutoPickCharacterState.Attempts > 0
                && Time.realtimeSinceStartup < AutoPickCharacterState.NextTryRealtime)
                return;

            int id = ResolvePickId();
            if (!AutoPickCharacterLogic.IsKnown(id) && id != AutoPickCharacterLogic.RandomId)
                Log.Warn<AutoPickCharacterModule>($"CharacterId={id} 不在当前角色表中，仍将发送");

            if (!ClientPackets.TrySend(new C_PICK_CHARACTER { CharacterId = id }, out string error))
            {
                Log.Warn<AutoPickCharacterModule>($"选角发送失败: {error}");
                AutoPickCharacterState.NextTryRealtime = Time.realtimeSinceStartup + interval;
                return;
            }

            AutoPickCharacterState.Attempts++;
            AutoPickCharacterState.NextTryRealtime = Time.realtimeSinceStartup + interval;
            Log.Info<AutoPickCharacterModule>(
                $"已发送 C_PICK_CHARACTER CharacterId={id}（{AutoPickCharacterLogic.GetDisplayName(id)}）" +
                $" 第{AutoPickCharacterState.Attempts}/{maxAttempts}次");
        }

        public static void SyncLobbyCharacter()
        {
            if (AutoPickCharacterModule.Mode == AutoPickMode.Random)
                return;

            int id = AutoPickCharacterModule.CharacterId;
            if (id == AutoPickCharacterLogic.RandomId)
                return;

            if (Time.realtimeSinceStartup < AutoPickCharacterState.NextLobbyTryRealtime)
                return;

            int maxLobbyAttempts = Mathf.Max(1, AutoPickCharacterModule.MaxAttempts);
            if (AutoPickCharacterState.LobbyAttempts >= maxLobbyAttempts)
            {
                if (!AutoPickCharacterState.LobbyGaveUpLogged)
                {
                    Log.Warn<AutoPickCharacterModule>(
                        $"大厅换角已达最大尝试次数 {maxLobbyAttempts}（对齐选角 MaxAttempts），停止同步；"
                        + "目标角色可能已被占用或被服务端拒绝，重新进大厅后可再试");
                    AutoPickCharacterState.LobbyGaveUpLogged = true;
                }
                return;
            }

            try
            {
                if (Managers.Player?.MyPlayer == null)
                {
                    AutoPickCharacterState.NextLobbyTryRealtime = Time.realtimeSinceStartup + 0.5f;
                    return;
                }

                int current = Managers.Player.MyPlayer.PublicInfo.CharacterId;
                if (current == id && AutoPickCharacterState.LastLobbySyncedId == id)
                    return;

                if (current == id)
                {
                    AutoPickCharacterState.LastLobbySyncedId = id;
                    return;
                }

                AutoPickCharacterState.LobbyAttempts++;
                if (!ClientPackets.TrySend(new C_MODIFY_PLAYER
                {
                    Type = EModifyPlayerEvent.ChangeCharacter,
                    Value = id
                }, out string error))
                {
                    Log.Warn<AutoPickCharacterModule>($"大厅换角失败（第 {AutoPickCharacterState.LobbyAttempts}/{maxLobbyAttempts} 次）: {error}");
                    AutoPickCharacterState.NextLobbyTryRealtime = Time.realtimeSinceStartup + 1.5f;
                    return;
                }

                AutoPickCharacterState.LastLobbySyncedId = id;
                AutoPickCharacterState.NextLobbyTryRealtime = Time.realtimeSinceStartup + 1.0f;
                Log.Info<AutoPickCharacterModule>(
                    $"大厅已请求换角 → {id}（{AutoPickCharacterLogic.GetDisplayName(id)}）"
                    + $" 第{AutoPickCharacterState.LobbyAttempts}/{maxLobbyAttempts}次");
            }
            catch (Exception ex)
            {
                Log.Warn<AutoPickCharacterModule>($"大厅换角异常: {ex.Message}");
                AutoPickCharacterState.NextLobbyTryRealtime = Time.realtimeSinceStartup + 2f;
            }
        }

        private static int ResolvePickId()
        {
            if (AutoPickCharacterModule.Mode == AutoPickMode.Random)
                return AutoPickCharacterLogic.RandomId;

            int fixedId = AutoPickCharacterModule.CharacterId;

            if (AutoPickCharacterModule.FallbackToRandom && AutoPickCharacterState.Attempts > 0)
            {
                int maxAttempts = Mathf.Max(1, AutoPickCharacterModule.MaxAttempts);
                int half = Math.Max(1, maxAttempts / 2);
                if (AutoPickCharacterState.Attempts >= half)
                {
                    if (AutoPickCharacterState.Attempts == half)
                        Log.Info<AutoPickCharacterModule>(
                            $"Fixed 目标可能被占用，后续尝试改用随机 ({AutoPickCharacterLogic.RandomId})");
                    return AutoPickCharacterLogic.RandomId;
                }
            }

            return fixedId;
        }
    }
}
