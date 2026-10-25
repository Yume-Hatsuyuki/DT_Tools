using System.Collections.Generic;
using System.Linq;
using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.LobbyMaxPlayers
{
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.GameStart))]
    internal static class LobbyMaxPlayersGameStartPatch
    {
        private static bool Prefix(GameRoom __instance)
        {
            if (!Engine.Enabled<LobbyMaxPlayersFeature>())
                return true;

            if (LobbyMaxPlayersFeature.MaxMembers <= 8)
                return true;

            ItemManager.Instance.ClearWithDespawn();

            __instance.CorpseMetas.Clear();
            __instance.RoundStartPlayerCount = __instance.Players.Count;
            Log.Info<LobbyMaxPlayersFeature>(
                $"本局人数 {__instance.RoundStartPlayerCount} → 黑幕击杀上限 {__instance.BlackKillLimit} 次（出生点循环复用）");

            foreach (Server.Game.Player player in __instance.Players)
                player.Clear();

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
                    player.GameStart(fallback);
            }
            else
            {
                list = Util.Shuffle(list, list.Count);
                int n = __instance.Players.Count;
                for (int i = 0; i < n; i++)
                    __instance.Players[i].GameStart(list[i % list.Count]);

                if (n > list.Count)
                    Log.Info<LobbyMaxPlayersFeature>(
                        $"玩家数 {n} 超过出生点数 {list.Count}，出生点循环复用");
            }

            Server.Game.DeviceManager.Instance.InitDevices();
            Server.Game.DeviceManager.Instance.InitStorage();

            InvokeMissionStart();

            __instance.RestoreFirstBloodVictimId(0);

            AwardManager.Instance.StartRound();

            return false;
        }

        private static void InvokeMissionStart()
        {
            if (!MissionAccess.StartParameterless())
                Log.Error<LobbyMaxPlayersFeature>("MissionManager.Instance / StartMission 不可用");
        }
    }
}
