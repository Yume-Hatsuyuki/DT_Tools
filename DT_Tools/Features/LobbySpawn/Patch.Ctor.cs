using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;
using Protocol;
using Server.Game;

namespace DT_Tools.Features.LobbySpawn
{
    /// <summary>
    /// 新玩家入场：构造后把初始位置从大厅点改为解析出的大厅点。
    /// </summary>
    /// <remarks>
    /// 原版构造：PublicInfo.Pos = MapData.LobbyPos 的副本（0.1.18c Server.Game/Player.cs:555），EffectivePosition 未设置（Player.cs:138）。
    /// 这里不调用 Move：Move 会广播重生包（Player.cs:747-752），而新玩家尚未加入 Players，且原版构造阶段也不经过 Move。
    /// 直接写入两处坐标，与 Move 对非存活玩家的处理（只更新坐标，不更新区域，Player.cs:733-746）等价。
    /// </remarks>
    [HarmonyPatch(typeof(Server.Game.Player), MethodType.Constructor,
        new[] { typeof(int), typeof(HostPeerSession), typeof(string) })]
    internal static class LobbySpawnCtorPatch
    {
        private static void Postfix(Server.Game.Player __instance)
        {
            if (!Engine.Enabled<LobbySpawnFeature>())
                return;
            if (__instance?.PublicInfo?.Pos == null)
                return;
            if (!LobbySpawnLogic.TryResolve(__instance.AccountID, out PosInfo pos))
                return;

            __instance.PublicInfo.Pos.X = pos.X;
            __instance.PublicInfo.Pos.Y = pos.Y;
            __instance.EffectivePosition.X = pos.X;
            __instance.EffectivePosition.Y = pos.Y;
            Log.Info<LobbySpawnFeature>($"{__instance.Name} 入场位置已覆盖（{pos.X:0}, {pos.Y:0}）。");
        }
    }
}
