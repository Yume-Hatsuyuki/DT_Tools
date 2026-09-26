using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// pin 同步：平板/HUD 的 pin 列表查找、白方强制刷新他人 pin 的每帧巡检、
    /// 常驻箭头的追踪过滤。贴图替换本体在 Ui。
    /// </summary>
    internal static class CharacterMapPinLogic
    {
        // ── 反射目标（私有字段，字符串定位）──
        /// <summary>平板 pin 列表：0.1.15b UI_GameTablet.cs:243。</summary>
        private static readonly FieldInfo TabletPinsField =
            AccessTools.Field(typeof(UI_GameTablet), "_subItems");

        /// <summary>HUD 小地图 pin 列表：0.1.15b UI_GameScene.cs:284。</summary>
        private static readonly FieldInfo HudPinsField =
            AccessTools.Field(typeof(UI_GameScene), "_playerPinList");

        // ── 私有方法反射缓存（白方巡检逐帧调用；HarmonyX 的 Traverse 无 MethodInfo 重载，
        //    且 Traverse 绑定目标实例、跨实例不能复用，故缓存开放实例委托）──
        /// <summary>RefreshPlayerPin(Player) 私有：0.1.15b UI_GameScene.cs:850。</summary>
        private static readonly Action<UI_GameScene, Player> RefreshPlayerPinOf =
            Bind<Action<UI_GameScene, Player>>(typeof(UI_GameScene), "RefreshPlayerPin", new[] { typeof(Player) });

        /// <summary>DeletePin(int) 私有：0.1.15b UI_GameScene.cs:933。</summary>
        private static readonly Action<UI_GameScene, int> DeletePinOf =
            Bind<Action<UI_GameScene, int>>(typeof(UI_GameScene), "DeletePin", new[] { typeof(int) });

        /// <summary>白方巡检的存活玩家 id 集合：静态复用，避免每帧 new 分配。</summary>
        private static readonly HashSet<int> LiveIds = new HashSet<int>();

        /// <summary>按名取私有实例方法并绑定为开放实例委托（游戏方法缺失时返回 null，调用点跳过）。</summary>
        private static T Bind<T>(Type owner, string name, Type[] parameters) where T : Delegate
        {
            MethodInfo mi = AccessTools.Method(owner, name, parameters);
            return mi == null ? null : (T)Delegate.CreateDelegate(typeof(T), null, mi);
        }

        public static UI_MinimapSubItem FindTabletPin(object tablet, int id)
            => FindPin(TabletPinsField, tablet, id);

        public static UI_MinimapSubItem FindHudPin(object scene, int id)
            => FindPin(HudPinsField, scene, id);

        private static UI_MinimapSubItem FindPin(FieldInfo listField, object owner, int id)
        {
            var list = listField?.GetValue(owner) as List<UI_MinimapSubItem>;
            if (list == null)
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ID == id)
                    return list[i];
            }
            return null;
        }

        /// <summary>HUD pin 列表原始访问（白方巡检清理 / 热关闭还原贴图用）。</summary>
        public static List<UI_MinimapSubItem> HudPinListOf(object scene)
            => HudPinsField?.GetValue(scene) as List<UI_MinimapSubItem>;

        /// <summary>平板 pin 列表原始访问（热关闭还原贴图用）。</summary>
        public static List<UI_MinimapSubItem> TabletPinListOf(object tablet)
            => TabletPinsField?.GetValue(tablet) as List<UI_MinimapSubItem>;

        /// <summary>
        /// 白方强制显示他人 Pin（独立开关 ShowPinsForWhite）：
        /// 原版 LateUpdate 在 Color==White 时直接 return（0.1.15b UI_GameScene.cs:819-822），
        /// 不调用 RefreshPlayerPin。开启后每帧补调 RefreshPlayerPin + ApplyPin，
        /// 并清理已死亡/离开的 pin（保留自己的 MyPlayer pin）。
        /// </summary>
        public static void SweepWhitePins(UI_GameScene scene)
        {
            MyPlayer my = Managers.Player.MyPlayer;
            if (my == null || my.Color != EPlayerColor.White)
                return;

            EGameState gs = Managers.Game.State;
            if (gs == EGameState.Trial || gs == EGameState.Lobby)
                return;

            LiveIds.Clear();
            LiveIds.Add(my.PublicInfo.PlayerId);
            foreach (Player player in Managers.Player.Players.Values)
            {
                int id = player.PublicInfo.PlayerId;
                if (id == my.PublicInfo.PlayerId)
                    continue;
                if (player.CharData == null || player.IsSpectator)
                    continue;
                if (Managers.Player.KnownDeadIds.Contains(id))
                    continue;

                LiveIds.Add(id);
                // RefreshPlayerPin(Player) 私有：0.1.15b UI_GameScene.cs:850（委托缓存在上方）
                RefreshPlayerPinOf?.Invoke(scene, player);

                UI_MinimapSubItem pin = FindHudPin(scene, id);
                if (pin != null)
                    CharacterMapPinUi.ApplyPin(pin, player, CharacterMapPinFeature.ReplaceBlackPin);
            }

            // 清掉已不在 live 列表里的他人 pin（DeletePin(int) 私有：0.1.15b UI_GameScene.cs:933）
            var pinList = HudPinListOf(scene);
            if (pinList == null)
                return;

            for (int i = pinList.Count - 1; i >= 0; i--)
            {
                UI_MinimapSubItem pin = pinList[i];
                if (pin == null || LiveIds.Contains(pin.ID))
                    continue;
                if (pin.Type == Define.EMinimapPinType.MyPlayer)
                    continue;
                DeletePinOf?.Invoke(scene, pin.ID);
            }
        }

        /// <summary>常驻箭头是否追踪该玩家：排除自己/无角色/观战/已死，Kaho 追踪目标不重复显示。</summary>
        public static bool ShouldTrack(MyPlayer my, Player player)
        {
            int id = player.PublicInfo.PlayerId;
            if (id == my.PublicInfo.PlayerId || player.CharData == null || player.IsSpectator)
                return false;
            if (Managers.Player.KnownDeadIds.Contains(id))
                return false;
            // Kaho 正在追踪该玩家时，原版技能箭头已存在，避免双箭头
            if (my.SkillData != null
                && my.SkillData.Type == ESkillType.ComplyRules
                && my.SkillState == -id)
                return false;
            return true;
        }
    }
}
