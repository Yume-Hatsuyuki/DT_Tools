using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>
    /// 输入分发层放行（"有按钮但按了没反应"的修复）：原版把 Special 键/杀按钮送进
    /// UseSabotageBase 的入口有三处，且都硬编码只认 Dark（或限 ChatDevice/Corpse/
    /// 销毁证据的 Black 分支）——0.1.16a MyPlayer.cs:1660-1699（InputInteract）、
    /// MyPlayer.cs:1256-1290（UpdateCarry）、UI_GameScene.cs:2044-2069（OnClickKillButton）。
    /// 本补丁在三处入口前拦截：档位放行的设备（Door/Fusebox/销毁证据目标）且提示激活时，
    /// 由任何放行颜色触发 UseSabotageBase（门发包 / 电闸开弹窗 / 销毁证据读条）；
    /// 其余情况原样放行，黑方的武器/暗招/销毁证据优先级在非放行设备上不受影响。
    /// 放行设备上破坏优先于武器/暗招——与屏幕上已亮出的"锁门/破坏 [Q]"提示一致。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), "InputInteract")]
    internal static class SabotageInputPatch
    {
        private static bool Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            if (!KeyBindings.Down(Define.EInputAction.Special))
                return true;    // 非 Special 帧：原版原样执行（含交互键/设备搜索）
            if (SabotageButtonUnlockLogic.TryDispatchExtended(__instance))
                return false;   // 已触发破坏：吞掉原版 Special 分支
            return true;
        }
    }

    /// <summary>搬运状态下的 Special 键（私有，字符串定位：MyPlayer.cs:1256）。</summary>
    [HarmonyPatch(typeof(MyPlayer), "UpdateCarry")]
    internal static class SabotageCarryInputPatch
    {
        private static bool Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            if (!KeyBindings.Down(Define.EInputAction.Special))
                return true;
            if (SabotageButtonUnlockLogic.TryDispatchExtended(__instance))
                return false;
            return true;
        }
    }

    /// <summary>杀按钮点击（私有，字符串定位：UI_GameScene.cs:2044）。</summary>
    [HarmonyPatch(typeof(UI_GameScene), "OnClickKillButton")]
    internal static class SabotageKillButtonPatch
    {
        private static bool Prefix()
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            var my = Managers.Player.MyPlayer;
            if (my == null)
                return true;
            if (SabotageButtonUnlockLogic.TryDispatchExtended(my))
                return false;
            return true;
        }
    }
}
