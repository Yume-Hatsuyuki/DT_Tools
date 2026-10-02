using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Patches.System.LobbyMaxPlayers
{
    /// <summary>
    /// GameStart 整替（0.1.16b GameRoom.cs:662-684）：StartPosList 打乱后分配，
    /// 人数超出出生点时 i % Count 循环复用，避免越界。仅当 MaxMembers &gt; 8 时接管，否则走原版。
    /// 与原版（0.1.16b GameRoom.cs:681 直接赋 FirstBloodVictimId=0）的差异用
    /// public RestoreFirstBloodVictimId（0.1.16b GameRoom.cs:167）等价实现。
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.GameStart))]
    internal static class LobbyMaxPlayersGameStartPatch
    {
        private static bool Prefix(GameRoom __instance)
        {
            if (!Engine.Enabled<LobbyMaxPlayersFeature>())
                return true;

            if (LobbyMaxPlayersFeature.MaxMembers <= 8)
                return true;

            ItemManager.Instance.ClearWithDespawn();            // public：0.1.16b ItemManager.cs:275

            __instance.CorpseMetas.Clear();                     // 0.1.16b GameRoom.cs:125
            __instance.RoundStartPlayerCount = __instance.Players.Count;  // 0.1.16b GameRoom.cs:129
            Log.Info<LobbyMaxPlayersFeature>(
                $"本局人数 {__instance.RoundStartPlayerCount} → 黑幕击杀上限 {__instance.BlackKillLimit} 次（出生点循环复用）");

            foreach (Server.Game.Player player in __instance.Players)
                player.Clear();                                 // 0.1.16b Server.Game/Player.cs:706

            List<PosInfo> list = Managers.Data.MapData.StartPosList != null
                ? Managers.Data.MapData.StartPosList.ToList()
                : new List<PosInfo>();

            if (list.Count == 0)
            {
                PosInfo fallback = Managers.Data.MapData.ErrorPos ?? Managers.Data.MapData.LobbyPos;
                if (fallback == null)
                    fallback = new PosInfo { X = 0f, Y = 0f };
                Log.Warn<LobbyMaxPlayersFeature>("StartPosList 为空，回退 ErrorPos/LobbyPos 出生点");
                foreach (Server.Game.Player player in __instance.Players)
                    player.GameStart(fallback);                 // 0.1.16b Server.Game/Player.cs:623
            }
            else
            {
                list = Util.Shuffle(list, list.Count);          // 0.1.16b Util.cs:521
                int n = __instance.Players.Count;
                for (int i = 0; i < n; i++)
                    __instance.Players[i].GameStart(list[i % list.Count]);

                if (n > list.Count)
                    Log.Info<LobbyMaxPlayersFeature>(
                        $"玩家数 {n} 超过出生点数 {list.Count}，出生点循环复用");
            }

            // 客户端全局命名空间另有 DeviceManager，必须用 Server.Game 全名
            Server.Game.DeviceManager.Instance.InitDevices();   // 0.1.16b Server.Game/DeviceManager.cs:798
            Server.Game.DeviceManager.Instance.InitStorage();   // 0.1.16b Server.Game/DeviceManager.cs:347

            InvokeMissionStart();

            // FirstBloodVictimId 只有 private set（0.1.16b GameRoom.cs:127），走 public Restore
            __instance.RestoreFirstBloodVictimId(0);            // 0.1.16b GameRoom.cs:167

            AwardManager.Instance.StartRound();                 // 0.1.16b Server.Game/AwardManager.cs:48（public）

            return false;
        }

        /// <summary>
        /// 开局任务分配：MissionManager 在发行程序集中为 internal（0.1.16b Server.Game/MissionManager.cs:10），
        /// 反射访问统一收敛到 Game/MissionAccess（无参 StartMission()：Server.Game/MissionManager.cs:355）。
        /// </summary>
        private static void InvokeMissionStart()
        {
            if (!MissionAccess.StartParameterless())
                Log.Error<LobbyMaxPlayersFeature>("MissionManager.Instance / StartMission 不可用");
        }
    }
}
