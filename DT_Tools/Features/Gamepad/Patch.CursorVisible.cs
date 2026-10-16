using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Features.Gamepad
{
    /// <summary>
    /// Managers.UpdateMouse 后缀（私有方法）。
    /// 依据：0.1.18c Managers.cs:243-266（UpdateMouse），字节码与 libs/Assembly-CSharp.dll 一致：鼠标键按下或位置变化时 Cursor.visible = true；
    /// 否则 _idleTimer 累加 Time.deltaTime（:263），满 3f 秒（:264）后 Cursor.visible = false（:266）。
    /// 手柄光标模式只靠右摇杆移动系统光标，摇杆静止时鼠标位置不变，光标会被游戏隐藏，表现为「调不出光标」。
    /// 后缀在原版之后把光标重新设为可见，同一帧生效、不产生闪烁。拍照模式（:245 Photo.IsPhotoMode）由 PhotoManager 自行控制光标，这里不干预。
    /// </summary>
    [HarmonyPatch(typeof(Managers), "UpdateMouse")]
    internal static class GamepadCursorVisiblePatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<GamepadFeature>())
                return;
            if (!GamepadService.CursorModeActive)
                return;
            var photo = Managers.Photo;
            if (photo != null && photo.IsPhotoMode)
                return;
            UnityEngine.Cursor.visible = true;
        }
    }
}
