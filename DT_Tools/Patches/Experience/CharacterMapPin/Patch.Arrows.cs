using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// HUD LateUpdate 后置（常驻角色箭头，独立开关 ShowCharacterArrow）。
    /// LateUpdate 私有：0.1.15b UI_GameScene.cs:802；构建/更新/回收在 Ui，
    /// 追踪过滤在 Logic.ShouldTrack。与本目录 WhitePins 补丁同挂一方法，互不影响。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameScene), "LateUpdate")]
    internal static class CharacterMapPinArrowsPatch
    {
        private static void Postfix()
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.ShowCharacterArrow)
            {
                CharacterMapPinUi.ClearAllArrows();
                return;
            }

            MyPlayer my = Managers.Player.MyPlayer;
            EGameState gs = Managers.Game.State;
            if (my == null || gs != EGameState.Survive && gs != EGameState.Detective)
            {
                CharacterMapPinUi.ClearAllArrows();
                return;
            }

            var liveIds = new HashSet<int>();
            foreach (Player player in Managers.Player.Players.Values)
            {
                if (!CharacterMapPinLogic.ShouldTrack(my, player))
                    continue;

                int id = player.PublicInfo.PlayerId;
                liveIds.Add(id);

                ArrowState state = CharacterMapPinUi.EnsureArrow(my, player);
                if (state != null)
                    CharacterMapPinUi.UpdateArrow(state, player);
            }

            CharacterMapPinUi.RemoveStaleArrows(liveIds);
        }
    }
}
