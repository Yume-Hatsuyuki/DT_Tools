using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 关闭房间阴影。原版 OnGhostVisualChanged：SetRoomShadowCasters(state != 1)。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "OnGhostVisualChanged")]
    [PatchFeature(
        section: "OnGhostVisualChanged",
        description: "移除迷雾：移除房间阴影效果。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class RemoveFogFeature
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            Managers.Map.SetRoomShadowCasters(false);
            return false;
        }
    }
}
