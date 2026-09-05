using HarmonyLib;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Void MyPlayer::OnGhostVisualChanged(System.Int32)
    /// 去除地图迷雾
    /// 原逻辑：state != 1 时才关闭阴影遮罩（仅鬼魂视角可见全图）
    /// 补丁逻辑：始终传入 false，无论 state 如何都关闭阴影遮罩
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "OnGhostVisualChanged")]
    [PatchConfig("Enable_Patch_RemoveFog", "去除地图迷雾：始终关闭房间阴影遮罩，无论当前状态如何都能看到完整地图，仅影响本地渲染。", author: "梦初雪")]
    internal static class Patch_RemoveFog
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            Managers.Map.SetRoomShadowCasters(false);

            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}
