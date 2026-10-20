using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.LobbySpawn
{
    /// <summary>
    /// 回大厅：把「回到大厅点」的移动改为解析出的大厅点。
    /// </summary>
    /// <remarks>
    /// 只替换传入的对象就是 MapData.LobbyPos 本身的调用（ReferenceEquals），即每轮结束回大厅（GameRoom.cs:868）。
    /// /beacon lobby 不受影响：Beacon 在客户端经 Teleport.TryTeleport 发送 C_MOVE，服务端收到的坐标对象是反序列化出的新对象，
    /// 不会与 MapData.LobbyPos 引用相等（Commands/Beacon/Command.cs:12 所述的客户端落位路径）。
    /// </remarks>
    [HarmonyPatch(typeof(Server.Game.Player), nameof(Server.Game.Player.Move))]
    internal static class LobbySpawnMovePatch
    {
        private static void Prefix(Server.Game.Player __instance, ref PosInfo pos)
        {
            if (!Engine.Enabled<LobbySpawnFeature>())
                return;
            if (__instance == null || pos == null || Managers.Data?.MapData == null)
                return;
            if (!ReferenceEquals(pos, Managers.Data.MapData.LobbyPos))
                return;
            if (LobbySpawnLogic.TryResolve(__instance.AccountID, out PosInfo target))
                pos = target;
        }
    }
}
