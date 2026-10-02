using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.AttackRangeServer
{
    /// <summary>
    /// Util.UnifiedAttackRect 取值后置（静态属性 getter，0.1.16a Util.cs:57）：
    /// 把攻击盒前向延伸 ExtraForwardReach。前向 = 盒 Pos.X 的负方向
    /// （未翻转时朝左，CalcRectInfo 对翻转侧取 -Pos.X，两个朝向天然对称：
    /// Pos.X -= N 与 Size.X += N 在任一朝向下都恰好把前缘外推 N、后缘不动）。
    /// 本属性共三个消费方：服务端 AttackPlayer（Server.Game/GameRoom.cs:2398）、
    /// 客户端挥击盒、以及白方假武器试人判定 GetFakeWeaponPlayer（MyPlayer.cs:2222）——
    /// ExtraForwardReach>0 时三者同步放大，其中假武器判定无法分离（同一静态属性），
    /// 即房主机上白方持假武器可命中/可试出的范围同步变宽，属已知取舍。
    /// </summary>
    [HarmonyPatch(typeof(Util), nameof(Util.UnifiedAttackRect), MethodType.Getter)]
    internal static class AttackRangeServerRectPatch
    {
        private static void Postfix(ref RectInfo __result)
        {
            if (!Engine.Enabled<AttackRangeServerFeature>())
                return;
            float extra = AttackRangeServerFeature.ExtraForwardReach;
            if (extra <= 0f)
                return;

            __result.Pos.X -= extra;
            __result.Size.X += extra;
        }
    }
}
