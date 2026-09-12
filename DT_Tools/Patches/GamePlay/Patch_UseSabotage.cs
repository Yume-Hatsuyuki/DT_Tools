using System;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   DeviceBase::UseSabotage(int index)
    ///
    /// <b>原版效果</b>：
    ///   Dark / Black 在可销毁证据的设备旁按 Q 时，读条时间在方法体内写死为 2.5f；
    ///   读条完成后播放 SabotageSfx，并向服务端发送 C_DESTROY_EVIDENCE。
    ///   仅当设备 HasOwnSabotage == false 且 WhatType != WNone（即 IsDestroyEvidenceTarget）
    ///   时才会执行到该基类实现；尸体 / 电话亭 / 电闸 / 军械库 / 门 / 通风管均重写了
    ///   UseSabotage 且 HasOwnSabotage == true，不会走到本补丁。
    ///
    /// <b>修改后效果</b>：
    ///   读条时间改由 [UseSabotage].CastingTime（默认 2.5）决定。
    ///   注意：本项只修改本地读条表现；销毁证据后的 30 秒冷却由服务端权威下发
    ///   （Server.Game.Device.DestroyEvidence → S_COOLTIME_DESTROY_EVIDENCE），不受影响。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版逻辑重写，仅替换 StartCasting 的时长来源。
    /// </summary>
    [HarmonyPatch(typeof(DeviceBase), "UseSabotage")]
    [PatchConfig(
        "UseSabotage",
        "天堂制造：时间要开始变化了！\n毁尸灭迹：可修改 Dark / Black 销毁证据（按 Q 破坏线索）的读条时长（默认 2.5 秒）。",
        author: "梦初雪")]
    internal static class Patch_UseSabotage
    {
        private static ConfigEntry<float> _castingTime;

        static Patch_UseSabotage()
        {
            _castingTime = Plugin.Instance.Config.Bind(
                "UseSabotage",
                "CastingTime",
                2.5f,
                new ConfigDescription("销毁证据（Dark / Black 按 Q 破坏线索）的读条时长，游戏默认为 2.5 秒。仅影响本地读条表现，不改变服务端 30 秒冷却。\n已验证：写0会无法读条，建议值>=0.1"));
        }

        [HarmonyPrefix]
        private static bool Prefix(DeviceBase __instance)
        {
            if (Managers.Game.CastingSlider != null)
                return false;

            float castTime = _castingTime.Value;
            if (castTime < 0.1f || float.IsNaN(castTime) || float.IsInfinity(castTime))
                castTime = 0.1f;

            Managers.Game.StartCasting(castTime, delegate
            {
                Managers.Sound.PlaySystem("SabotageSfx");
                Managers.Network.GameServer.Send(new C_DESTROY_EVIDENCE
                {
                    DeviceId = __instance.ID
                });
            });

            return false;
        }
    }
}
