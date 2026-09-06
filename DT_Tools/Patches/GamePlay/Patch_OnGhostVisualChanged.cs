using HarmonyLib;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   MyPlayer::OnGhostVisualChanged(int state)
    ///
    /// <b>原版效果</b>：
    ///   Managers.Map.SetRoomShadowCasters(state != 1);
    ///   仅 state == 1 时关闭房间阴影。
    ///
    /// <b>修改后效果</b>：
    ///   始终 SetRoomShadowCasters(false)，本机房间阴影关闭。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 直接调用 SetRoomShadowCasters(false)。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "OnGhostVisualChanged")]
    [PatchConfig(
        "Enable_OnGhostVisualChanged",
        "移除房间迷雾：本地始终显示完整地图，仅影响本机画面。",
        author: "梦初雪")]
    internal static class Patch_OnGhostVisualChanged
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            Managers.Map.SetRoomShadowCasters(false);
            return false;
        }
    }
}
