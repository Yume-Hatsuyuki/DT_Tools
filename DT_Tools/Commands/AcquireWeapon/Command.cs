using DT_Tools.Commands;

namespace DT_Tools.Commands.AcquireWeapon
{
    /// <summary>
    /// /acquire_weapon [#armoryId] — 天匠：本机玩家无视距离从当前开放的武器架取得凶器
    /// （客户端发包，跟随控制台，非房主可用）。
    ///
    /// 用法：
    ///   无参数 → 列出全部武器架的 ID、所在地区、状态与换点倒计时（只读，不发包），
    ///             其中开放刀架的 ID 可直接用于 /天匠 #id 拔刀。
    ///   带参数  → 从指定武器架拔刀（无视距离）。
    ///
    /// 权限 / 前置条件（拔刀分支）：
    ///   - 本机已进入对局且处于生存阶段；
    ///   - 按本机颜色自动选择原版交互类型（0.1.15b Server.Game/Armory.cs:44-48 分流）：
    ///       White → AcquireWeapon（服务端 Armory.AcquireWeapon：State==1 && Color==White，:112）
    ///       Dark  → SelectBlack（服务端 AcquireWeaponDark：State==1 && Color==Dark
    ///                             && Weapon==null && !WeaponPickupLocked，:122-124）
    ///   - Black（已经是持刀者）直接拒绝——服务端两个分支都不接受 Black。
    ///   - 服务端 SendWeapon 硬断言目标刀架 == 当前凶器架 CurrentArmory
    ///     （0.1.15b Server.Game/Armory.cs:132-134，Log.Assert 失败即整段跳过；
    ///     CurrentArmory 定义见 Server.Game/DeviceManager.cs:42），故对非当前
    ///     开放的凶器架发包会被静默拒——服务端仅留一条错误日志，本机无回执。
    ///
    /// 实现原理：原版客户端 Armory.InteractArmory / UseSabotageArmory 要求本地物理接触
    /// （SearchInteractDevice 矩形判定）才发 C_INTERACT_ARMORY；而服务端
    /// DeviceManager.Interact → Armory 全程无距离校验，故直接构造包经
    /// Managers.Network.GameServer.Send 即可在地图任意位置拔刀。
    /// 武器架状态经 S_MODIFY_DEVICE 全员广播，客户端可见；地区取 DeviceData.RoomType
    /// → TextDic 本地化（与平板扫描 UI_GameTablet.SetWeaponInfo 同源）。
    ///
    /// 示例:
    ///   /天匠                  列出全部刀架信息
    ///   /acquire_weapon #12    从刀架 #12 拔刀
    /// </summary>
    internal sealed class AcquireWeaponCommand : ICommand
    {
        public string Name => "acquire_weapon";
        public string[] Aliases => new[] { "天匠" };
        public string Usage => "acquire_weapon [#armoryId]";
        public string Description => "天匠：无参数列出武器架信息（含真伪可拔判定），带参数无视距离拔刀（对开放刀架生效；房主启用武器架多刀时全部开放架可拔）。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            // ── 无参数：列出全部刀架信息 ──
            if (ctx.Args.Length == 0)
                return AcquireWeaponLogic.ListArmories(ctx);

            // ── 带参数：执行拔刀 ──
            return AcquireWeaponLogic.Acquire(ctx, ctx.Args[0]);
        }
    }
}
