using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 大厅移速。仅在 Lobby 下改写 FixedUpdateMove 的 deltaSpeed（原版默认 1）。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "FixedUpdateMove")]
    [PatchFeature(
        section: "FixedUpdate",
        description: "大厅移速调整：可修改大厅内移动倍率（默认 1.0）。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class LobbySpeedFeature
    {
        [ConfigField(1.0f, "大厅内移动速度倍率，游戏默认为 1.0。")]
        public static ConfigEntry<float> LobbyDeltaSpeed;

        [HarmonyPrefix]
        private static void Prefix(ref float deltaSpeed)
        {
            if (!FeatureGate.Enabled(typeof(LobbySpeedFeature)))
                return;

            if (Managers.Game.State == EGameState.Lobby)
                deltaSpeed = LobbyDeltaSpeed.Value;
        }
    }
}
