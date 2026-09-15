using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 本机名字常显。ChangeMyPlayer 切到自己且非 Lobby 时强制 NameTag 开启；可选死后仍显。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "ChangeMyPlayer",
        description: "吾之真名：进入游戏后头顶名字持续可见。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class ShowTrueNameFeature
    {
        [ConfigField(false, "死亡后是否继续显示自己头顶名字（默认 false）。")]
        public static ConfigEntry<bool> ShowWhileDead;

        [HarmonyPatch(typeof(PlayerManager), "ChangeMyPlayer")]
        [HarmonyPostfix]
        private static void PostfixChangeMyPlayer(int playerId)
        {
            Player my = Managers.Player.MyPlayer;
            if (my?.NameTag == null || playerId != my.PublicInfo.PlayerId)
                return;
            EGameState st = Managers.Game.State;
            if (st == EGameState.Lobby || st == EGameState.NoneState)
                return;
            my.NameTag.gameObject.SetActive(true);
        }

        [HarmonyPatch(typeof(GameManagerEX), "Dead")]
        [HarmonyPostfix]
        private static void PostfixDead()
        {
            if (!ShowWhileDead.Value)
                return;
            Player my = Managers.Player.MyPlayer;
            if (my?.NameTag != null)
                my.NameTag.gameObject.SetActive(true);
        }
    }
}
