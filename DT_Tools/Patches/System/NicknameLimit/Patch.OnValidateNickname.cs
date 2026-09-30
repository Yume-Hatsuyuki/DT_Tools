using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Patches.System.NicknameLimit
{
    /// <summary>
    /// OnValidateNickname 整替：保留「写 MyPlayerName + Prefs」与非法字符拦截，
    /// 只移除权重 &gt;12 一档。私有方法，字符串定位：0.1.15b UI_LobbyScene.cs:1559；
    /// 私有字段 _nicknameKey：0.1.15b UI_LobbyScene.cs:390。
    /// </summary>
    [HarmonyPatch(typeof(UI_LobbyScene), "OnValidateNickname")]
    internal static class NicknameLimitValidatePatch
    {
        private static bool Prefix(UI_LobbyScene __instance, string name)
        {
            if (!Engine.Enabled<NicknameLimitFeature>())
                return true;

            NicknameLimitLogic.LiftInputHardLimit(__instance, NicknameLimitLogic.GetNicknameField(__instance));

            // 与原版一致（0.1.15b UI_LobbyScene.cs:1561-1565）：先写入；我们仅放宽长度，
            // 非法名仍可被写进 Prefs，但门禁 _isNickNameValid 会挡住进房。
            Managers.Player.MyPlayerName = name;
            string key = Traverse.Create(__instance).Field("_nicknameKey").GetValue<string>();
            if (key != null)
                PlayerPrefs.SetString(key, name);

            bool ok = NicknameLimitLogic.IsNameAcceptable(name, out string tip);
            NicknameLimitLogic.ApplyValidUi(__instance, ok, tip ?? "");
            Log.Info<NicknameLimitFeature>(
                ok ? $"校验通过 len={name?.Length ?? 0}" : $"校验失败: {tip}");
            return false;
        }
    }
}
