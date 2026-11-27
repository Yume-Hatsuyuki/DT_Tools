// 作用：为跨版本联机挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Features.CrossVersionPlay
{
    [HarmonyPatch(typeof(SteamLobbyManager), nameof(SteamLobbyManager.GetMyBuildId))]
    internal static class CrossVersionPlayPatch
    {
        private const string NonSteamBuildId = "0";

        private static string _lastLoggedCompatId;

        private static void Postfix(ref string __result)
        {
            if (!Engine.Enabled<CrossVersionPlayFeature>())
                return;
            if (__result == NonSteamBuildId)
                return;

            string compatId = (CrossVersionPlayFeature.CompatBuildId ?? string.Empty).Trim();
            if (compatId.Length == 0)
                return;

            if (compatId != _lastLoggedCompatId)
            {
                _lastLoggedCompatId = compatId;
                Log.Info<CrossVersionPlayFeature>($"大厅版本标识改为 {compatId}（游戏原值 {__result}）。");
            }
            __result = compatId;
        }
    }
}
