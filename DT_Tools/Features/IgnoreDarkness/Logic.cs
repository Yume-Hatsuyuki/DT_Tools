using UnityEngine;

namespace DT_Tools.Features.IgnoreDarkness
{
    internal static class IgnoreDarknessLogic
    {
        public static void ApplyLights()
        {
            if (Managers.Game == null)
                return;

            try
            {
                Managers.Game.ApplyDarkness();
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<IgnoreDarknessFeature>(ex, "ApplyDarkness 重应用失败");
            }
        }

        public static void ForceLit()
        {
            DeviceBase cameraTarget = Managers.Game.CameraTarget;
            if (cameraTarget != null)
                cameraTarget.SetLight(isActive: false);

            Camera cam = Camera.main;
            if (cam != null)
            {
                FollowCamera follow = cam.GetComponent<FollowCamera>();
                if (follow != null)
                    follow.SetLight(active: true);
            }
        }
    }
}
