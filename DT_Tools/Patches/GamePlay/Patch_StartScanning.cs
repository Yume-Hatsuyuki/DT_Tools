using System;
using BepInEx.Configuration;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   GameManagerEX::StartScanning(float castingTime, Action callback)
    ///
    /// <b>原版效果</b>：
    ///   传入的 castingTime 固定为调用方写死的 2f；若本地带有 ScanUp（Miyuki 技能
    ///   DetailCheck 触发的隐藏 BUFF），强制覆盖为 0.5f。
    ///
    /// <b>修改后效果</b>：
    ///   castingTime 改由 [StartScanning].CastingTime（默认 2.0）决定，忽略调用方传入值；
    ///   同时去掉 IsScanUp 的强制覆盖分支。
    ///   原因：玩家一旦自行配置了读条时间，服务端下发的 ScanUp（默认把读条钳到 0.5s）
    ///   反而会在配置时长 &lt; 0.5s 时把“加速”变成“减速”，等同于变相 debuff。
    ///   去掉该判断后，本地配置的时长即为最终时长，不再受 ScanUp 是否存在影响。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版逻辑重写，仅替换 castingTime 来源、移除 IsScanUp 分支。
    /// </summary>
    [HarmonyPatch(typeof(GameManagerEX), "StartScanning")]
    [PatchConfig(
        "StartScanning",
        "天堂制造：时间要开始变化了！\n扫描读条时间：可在本段修改默认搜索（扫描）读条时长（默认 2.0 秒），并忽略 ScanUp 的强制覆盖。",
        author: "梦初雪")]
    internal static class Patch_StartScanning
    {
        private static ConfigEntry<float> _castingTime;

        static Patch_StartScanning()
        {
            _castingTime = Plugin.Instance.Config.Bind(
                "StartScanning",
                "CastingTime",
                2.0f,
                new ConfigDescription("扫描（搜索线索）读条时长，游戏默认为 2.0 秒；ScanUp 会将其覆盖为 0.5 秒，本补丁忽略该覆盖。\n已验证：写0会无法读条，建议值>=0.1"));
        }

        [HarmonyPrefix]
        private static bool Prefix(GameManagerEX __instance, ref float castingTime, Action callback)
        {
            if (__instance.ScanningSlider != null)
                return false;

            float castTime = _castingTime.Value;
            if (castTime < 0.1f || float.IsNaN(castTime) || float.IsInfinity(castTime))
                castTime = 0.1f;

            castingTime = castTime;

            Managers.Sound.PlayLoop("ScanningSfx");
            MyPlayer myPlayer = Managers.Player.MyPlayer;
            if (__instance.IsAlive)
            {
                myPlayer.ChangeMyPlayerState(EPlayerState.Scanning);
            }

            UI_ScanningSlider uI_ScanningSlider = Managers.UI.MakeWorldSpaceUI<UI_ScanningSlider>(myPlayer.UIGroup);
            uI_ScanningSlider.SetInfo(castingTime, callback);
            uI_ScanningSlider.transform.localPosition = new Vector2(0f, -30f);

            // ScanningSlider 是 { get; private set; }，通过反射写回
            Traverse.Create(__instance).Property("ScanningSlider").SetValue(uI_ScanningSlider);

            return false;
        }
    }
}
