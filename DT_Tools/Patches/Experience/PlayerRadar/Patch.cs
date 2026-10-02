using System;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.PlayerRadar
{
    /// <summary>
    /// LateUpdate 整替：复刻 0.1.16b UI_GameTablet.cs:2992，去掉
    /// 「myPlayer.Color == White && Managers.Game.IsAlive 则不刷新他人」分支（:3004）。
    /// 方法为 private，字符串定位：0.1.16b UI_GameTablet.cs:2992；
    /// _init 为基类 InitBase 的 protected 字段（0.1.16b InitBase.cs:5，AccessTools.Field
    /// 已实测沿基类链命中）；RefreshMyPlayerPin（:1200）/ RefreshPlayerPin(Player)（:1211）
    /// 为 private——逐帧热路径，反射缓存策略见 Core/Reflect.cs 头注释。
    ///
    /// 与 CharacterMapPin 的隐式联动：本补丁经委托调用原 RefreshPlayerPin，仍会触发
    /// CharacterMapPin 挂在其上的 Postfix（头像替换）——该联动靠补丁机制维持，
    /// 若本补丁改为不再调用原方法，CharacterMapPin 的平板 pin 头像会静默失效。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "LateUpdate")]
    internal static class PlayerRadarPatch
    {
        /// <summary>_init：0.1.16b InitBase.cs:5，protected（AccessTools.Field 沿基类链查找）。</summary>
        private static readonly FieldInfo InitField = AccessTools.Field(typeof(UI_GameTablet), "_init");

        /// <summary>RefreshMyPlayerPin() 私有：0.1.16b UI_GameTablet.cs:1200。</summary>
        private static readonly Action<UI_GameTablet> RefreshMyPlayerPinOf =
            Reflect.Bind<Action<UI_GameTablet>>(typeof(UI_GameTablet), "RefreshMyPlayerPin");

        /// <summary>RefreshPlayerPin(Player) 私有：0.1.16b UI_GameTablet.cs:1211。</summary>
        private static readonly Action<UI_GameTablet, Player> RefreshPlayerPinOf =
            Reflect.Bind<Action<UI_GameTablet, Player>>(typeof(UI_GameTablet), "RefreshPlayerPin", new[] { typeof(Player) });

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
