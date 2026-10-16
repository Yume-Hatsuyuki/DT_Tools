using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.ShowTrueName
{
    /// <summary>
    /// ChangeMyPlayer 后置：原版在非 Lobby 且切到自己时把 NameTag 关闭
    /// （0.1.18c PlayerManager.cs)，这里对非 Lobby 阶段强制打开。
    /// </summary>
    /// <remarks>
    /// 公共方法（nameof 定位）；Player.NameTag 属性：0.1.18c Player.cs:337。
    /// </remarks>
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
