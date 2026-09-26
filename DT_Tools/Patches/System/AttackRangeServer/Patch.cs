using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.System.AttackRangeServer
{
    /// <summary>
    /// Util.UnifiedAttackRect 取值后置（静态属性 getter，0.1.15b Util.cs:57）：
    /// 把攻击盒前向延伸 ExtraForwardReach。前向 = 盒 Pos.X 的负方向
    /// （未翻转时朝左，CalcRectInfo 对翻转侧取 -Pos.X，两个朝向天然对称：
    /// Pos.X -= N 与 Size.X += N 在任一朝向下都恰好把前缘外推 N、后缘不动）。
    /// 服务端 AttackPlayer（GameRoom.cs:2398）与客户端挥击盒（MyPlayer.cs:2222）
    /// 共用本属性——开启者（房主机）两端同步放大，保持判定一致。
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
