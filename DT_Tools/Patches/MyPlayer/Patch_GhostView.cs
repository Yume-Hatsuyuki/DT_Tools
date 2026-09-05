using HarmonyLib;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Boolean MyPlayer::IsGhostView()
    /// 化身幽灵视角
    /// 原逻辑：仅在玩家死亡（!IsAlive）时返回 true
    /// 补丁逻辑：始终返回 true，活着也以幽灵身份进入游戏
    /// ⚠危险功能：可能导致异常游戏状态，不建议在正常对局中开启
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.IsGhostView))]
    [PatchConfig("Enable_Patch_GhostView", "⚠危险 化身幽灵视角：强制以幽灵身份进入游戏，活着也能看到幽灵视角内容。\n可能导致异常游戏状态，不建议在正常对局中开启。", author: "梦初雪")]
    internal static class Patch_GhostView
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}
