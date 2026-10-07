using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.ForceRinPick
{
    /// <summary>
    /// 选角拦截（0.1.16b GameRoom.PickCharacter(Player, int)，服务端）：被标记玩家选角时
    /// 把 CharacterId 参数改写为目标角色 DataId（仅本次，标记即取即删）。
    /// 注意：若目标角色已被其他玩家选走，服务端角色去重校验会拒绝，本次强制静默无效。
    /// </summary>
    [HarmonyPatch(typeof(Server.Game.GameRoom), "PickCharacter")]
    internal static class ForceRinPickPatch
    {
        private static void Prefix(Server.Game.GameRoom __instance, Server.Game.Player player, ref int characterId)
        {
            try
            {
                if (!Engine.Enabled<ForceRinPickFeature>())
                    return;
                if (player == null)
                    return;

                int forcedId = ForceRinPickLogic.Consume(player.PublicInfo.PlayerId);
                if (forcedId <= 0)
                    return;

                characterId = forcedId;
                Log.Info<ForceRinPickFeature>($"强制选角生效：玩家 {player.PublicInfo.PlayerId}（{player.Name}）→ 角色 {forcedId}");
            }
            catch (global::System.Exception ex)
            {
                Log.Warn<ForceRinPickFeature>("强制选角改写失败（可忽略）：" + ex.Message);
            }
        }
    }
}
