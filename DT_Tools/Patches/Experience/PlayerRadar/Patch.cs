using System;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.PlayerRadar
{
    /// <summary>
    /// LateUpdate 整替：复刻 0.1.15b UI_GameTablet.cs:2992，去掉
    /// 「myPlayer.Color == White && Managers.Game.IsAlive 则不刷新他人」分支（:3004）。
    /// 方法为 private，字符串定位：0.1.15b UI_GameTablet.cs:2992；
    /// _init 为基类 InitBase 的 protected 字段（0.1.15b InitBase.cs:5），
    /// RefreshMyPlayerPin :1200 / RefreshPlayerPin(Player) :1211 为 private——
    /// 均 LateUpdate 逐帧调用，缓存 FieldInfo 与开放实例委托（HarmonyX 的 Traverse 无
    /// MethodInfo 重载；Traverse 绑定目标实例，跨实例也不能缓存 Traverse 本身，
    /// 故缓存 MemberInfo 并 CreateDelegate）。已实测 AccessTools.Field 沿基类链查找，可命中基类字段。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    internal static class PlayerRadarPatch
    {
        /// <summary>_init：0.1.15b InitBase.cs:5，protected（AccessTools.Field 沿基类链查找）。</summary>
        private static readonly FieldInfo InitField = AccessTools.Field(typeof(UI_GameTablet), "_init");

        /// <summary>RefreshMyPlayerPin() 私有：0.1.15b UI_GameTablet.cs:1200。</summary>
        private static readonly Action<UI_GameTablet> RefreshMyPlayerPinOf =
            Bind<Action<UI_GameTablet>>(typeof(UI_GameTablet), "RefreshMyPlayerPin");

        /// <summary>RefreshPlayerPin(Player) 私有：0.1.15b UI_GameTablet.cs:1211。</summary>
        private static readonly Action<UI_GameTablet, Player> RefreshPlayerPinOf =
            Bind<Action<UI_GameTablet, Player>>(typeof(UI_GameTablet), "RefreshPlayerPin", new[] { typeof(Player) });

        /// <summary>按名取私有实例方法并绑定为开放实例委托（游戏方法缺失时返回 null，前缀退回原版）。</summary>
        private static T Bind<T>(Type owner, string name, Type[] parameters = null) where T : Delegate
        {
            MethodInfo mi = AccessTools.Method(owner, name, parameters);
            return mi == null ? null : (T)Delegate.CreateDelegate(typeof(T), null, mi);
        }

        private static bool Prefix(UI_GameTablet __instance)
        {
            if (!Engine.Enabled<PlayerRadarFeature>())
                return true;

            // 反射目标漂移（游戏升级改签名）时整体退回原版行为，不做半套整替
            if (InitField == null || RefreshMyPlayerPinOf == null || RefreshPlayerPinOf == null)
                return true;

            if (!(bool)InitField.GetValue(__instance))
                return false;

            if (Managers.Player.MyPlayer == null || Managers.Game.State == EGameState.Trial)
                return false;

            RefreshMyPlayerPinOf(__instance);
            foreach (Player player in Managers.Player.Players.Values)
                RefreshPlayerPinOf(__instance, player);

            return false;
        }
    }
}
