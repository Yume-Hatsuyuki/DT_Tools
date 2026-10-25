using DT_Tools.Core;
using DT_Tools.Game;
using HarmonyLib;

namespace DT_Tools.Features.Gamepad
{
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
