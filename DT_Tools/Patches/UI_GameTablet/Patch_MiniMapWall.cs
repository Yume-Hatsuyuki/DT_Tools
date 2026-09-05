using HarmonyLib;
using Protocol;
using System.Collections.Generic;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Void UI_GameTablet::LateUpdate()
    /// 小地图透视
    /// 原逻辑：只有白色玩家或死亡状态才刷新其他玩家的位置
    /// 补丁逻辑：始终刷新所有玩家位置
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    [PatchConfig("Enable_Patch_MiniMapWall", "小地图透视：删除白方限制，始终刷新所有玩家在小地图上的位置标记，包括黑方玩家。", author: "梦初雪")]
    internal static class Patch_MiniMapWall
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_GameTablet __instance)
        {
            // _init 定义在祖父类 InitBase，Harmony 字段注入不稳定，改用 Traverse 读取
            var t = Traverse.Create(__instance);

            if (!t.Field("_init").GetValue<bool>()) return true;

            if (Managers.Player.MyPlayer == null) return true;

            // 审判阶段不刷新（原逻辑保留）
            if (Managers.Game.State == EGameState.Trial) return true;

            // RefreshMyPlayerPin / RefreshPlayerPin 是 private，用 Traverse 调用
            t.Method("RefreshMyPlayerPin").GetValue();

            // ── 核心改动：删除 Color != White 的限制，始终刷新所有人 ──
            foreach (Player player in Managers.Player.Players.Values)
            {
                t.Method("RefreshPlayerPin", player).GetValue();
            }

            return false; // 原方法已被完整替代，不再执行原方法
        }
    }
}