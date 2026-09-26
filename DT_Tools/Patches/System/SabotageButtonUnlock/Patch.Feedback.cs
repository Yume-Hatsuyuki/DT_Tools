using System.Collections;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.System.SabotageButtonUnlock
{
    /// <summary>
    /// 锁门失败反馈：非黑幕的锁门请求在服务端（Door.HandleEvent，Door.cs:53）只认黑幕，
    /// 房主未启用「DoorLockServer」时请求会被静默拒绝——发包与 2 秒内未收到
    /// S_COOLTIME_SABOTAGE 回执（成功时服务端发回给锁门者，Door.cs:60-62）即本地提示。
    /// Door.UseSabotage 为 protected（Door.cs:347）——字符串定位。
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
}
