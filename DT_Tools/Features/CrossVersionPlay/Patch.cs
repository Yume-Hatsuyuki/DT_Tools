using DT_Tools.Core;
using DummyClient;
using HarmonyLib;

namespace DT_Tools.Features.CrossVersionPlay
{
    /// <summary>
    /// SteamLobbyManager.GetMyBuildId 后缀：把原版返回的版本标识换成 CrossVersionPlayFeature.CompatBuildId。
    /// </summary>
    /// <remarks>
    /// 依据：0.1.20a DummyClient/SteamLobbyManager.cs:153-160（已核对，libs/Assembly-CSharp.dll 含同名方法与 "0.1.20" 字面量）。
    /// 非 Steam 启动时原版返回 "0"（:159），此时不改写，保持原行为。
    /// 配置为空白时不改写，等同于功能关闭。
    /// </remarks>
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
