using UnityEngine;

namespace DT_Tools.Patches.Experience.IgnoreDarkness
{
    /// <summary>开关热切换时按当前 Darkness 重算灯光（走被补丁的 ApplyDarkness）。</summary>
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
                Log.Warn<IgnoreDarknessFeature>("ApplyDarkness 重应用失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 强制「有光」表现：关玩家点光源、开摄像机全局光。
        /// 对齐 ApplyDarkness 在 flag=false 时的分支（0.1.16b GameManagerEX.cs:260-267）。
        /// </summary>
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
