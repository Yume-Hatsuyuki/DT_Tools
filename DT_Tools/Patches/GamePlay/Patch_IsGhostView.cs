using HarmonyLib;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   MyPlayer::IsGhostView()
    ///
    /// <b>原版效果</b>：
    ///   return !Managers.Game.IsAlive;
    ///
    /// <b>修改后效果</b>：
    ///   固定返回 true（存活时也走幽灵视角分支）。
    ///   可能与部分状态机假设冲突，建议仅本地调试使用。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 写回 __result = true。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.IsGhostView))]
    [PatchConfig(
        "Enable_IsGhostView",
        "幽灵视角：存活时也以幽灵视角进入。可能影响正常对局，建议仅本地调试使用。",
        author: "梦初雪")]
    internal static class Patch_IsGhostView
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
