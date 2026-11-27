// 作用：为吾之真名挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.ShowTrueName
{
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ChangeMyPlayer))]
    internal static class ShowTrueNameChangeMyPlayerPatch
    {
        private static void Postfix(int playerId)
        {
            if (!Engine.Enabled<ShowTrueNameFeature>())
                return;

            Player my = Managers.Player.MyPlayer;
            if (my?.NameTag == null || playerId != my.PublicInfo.PlayerId)
                return;
            EGameState st = Managers.Game.State;
            if (st == EGameState.Lobby || st == EGameState.NoneState)
                return;
            my.NameTag.gameObject.SetActive(true);
        }
    }
}
