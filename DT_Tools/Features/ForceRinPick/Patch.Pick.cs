using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.ForceRinPick
{
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
                Log.Warn<ForceRinPickFeature>(ex, "强制选角改写失败（可忽略）");
            }
        }
    }
}
