using System.Collections;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>
    /// 锁门失败反馈：非黑幕的锁门请求在服务端（Server.Game/Door.cs:63 HandleEvent）只认黑幕，
    /// 房主未启用「DoorLockServer」时请求会被静默拒绝——发包与 2 秒内未收到
    /// S_COOLTIME_SABOTAGE 回执（成功时服务端发回给锁门者，Server.Game/Door.cs:74）即本地提示。
    /// Door.UseSabotage 为 protected（全局 Door.cs:347）——字符串定位。
    /// 注意：0.1.15b 存在全局 Door.cs 与 Server.Game/Door.cs 两个同名文件，行号锚点必须带子目录。
    /// </summary>
    [HarmonyPatch(typeof(Door), "UseSabotage")]
    internal static class DoorLockAttemptPatch
    {
        private static void Prefix()
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return;
            if (Managers.Player.MyPlayer.Color == EPlayerColor.Dark)
                return;    // 黑幕原版必成功，无需反馈

            SabotageButtonUnlockLogic.RecordLockAttempt();
        }
    }

    /// <summary>成功回执记录（Handle_S_COOLTIME_SABOTAGE：PacketHandler.cs:769-774）。
    /// PacketHandler 为 internal（PacketHandler.cs:9）——TargetMethod 运行时解析。</summary>
    [HarmonyPatch]
    internal static class SabotageCooltimeAckPatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_COOLTIME_SABOTAGE");

        private static void Postfix()
        {
            SabotageButtonUnlockLogic.RecordLockAck();
        }
    }

    /// <summary>销毁证据回执记录（Handle_S_COOLTIME_DESTROY_EVIDENCE：PacketHandler.cs:772）——
    /// 白方放行档位下的销毁尝试据此判定房主是否受理（同上，TargetMethod 运行时解析）。</summary>
    [HarmonyPatch]
    internal static class DestroyEvidenceAckPatch
    {
        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("PacketHandler"), "Handle_S_COOLTIME_DESTROY_EVIDENCE");

        private static void Postfix()
        {
            SabotageButtonUnlockLogic.RecordDestroyAck();
        }
    }
}
