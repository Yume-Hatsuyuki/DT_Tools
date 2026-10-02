using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Patches.System.DoorLockServer
{
    /// <summary>
    /// 锁门服务端：Door.HandleEvent 原版仅黑幕可锁门（0.1.16b Server.Game/Door.cs:66），
    /// 本功能按 LockDoorMode 额外放行黑方/白方/所有身份，并调整锁门冷却间隔。
    /// 锁门行为与原版一致不留任何线索（白方锁门留痕试验已移除：门的 RoomID 为 0，
    /// 写入的线索会让平板重建时报 "0 is not exist room"，且原版锁门本就不留证据）。
    /// 冷却双落点必须同值：原方法三处 40（Door.cs:69/70/76，经本功能 Transpiler 替换）
    /// 与放行复刻序列（Patch.cs 前缀逐句镜像原版，直接读 Interval）——放行身份走复刻、
    /// 未放行身份走原方法，两路不同值会出现同人不同 CD。迁移快照剩余时长
    /// （Server.Game/SnapshotCodec.cs:98 用 EndTick-surviveTime 反推）与客户端 UI
    /// （S_COOLTIME_SABOTAGE 包值倒计时）自动跟随。
    /// Transpiler 是装载期 IL 改写、无法按调用门闩：自管 Harmony（存在 SelfHarmony 字段
    /// 引擎即跳过本命名空间自动挂载），Enabled 热切换时按需挂/卸整个补丁类，关闭即恢复原版。
    /// 客户端按钮显示由「SabotageButtonUnlock」负责；进别人房间时能否锁门取决于
    /// 房主是否启用本功能（判定在房主机）。
    /// </summary>
    [PatchFeature(
        "锁门服务端：LockDoorMode 选额外放行谁（Dark=默认原版仅黑幕；Black=黑幕+黑方；White=黑幕+白方；All=全部）；锁门冷却间隔 Interval 可调（原版 40s，0=无冷却）。\n进别人房间时取决于房主是否启用本功能；客户端按钮显示见「SabotageButtonUnlock」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Author = "梦初雪")]
    public sealed class DoorLockServerFeature
    {
        [Config("锁门额外放行身份：Dark=默认（原版档位，仅黑幕）；Black=黑幕+黑方；White=黑幕+白方；All=全部。")]
        public static LockDoorMode Mode = LockDoorMode.Black;

        [Config("锁门冷却间隔（秒）：多久可以再次锁门（服务端判定，客户端倒计时自动跟随）。0=无冷却。原版 40。")]
        public static int Interval = 40;

        /// <summary>存在本字段时引擎跳过本命名空间自动挂载，前缀+Transpiler 由本类按 Enabled 自管挂卸。</summary>
        internal static Harmony SelfHarmony;

        private static bool _patched;

        /// <summary>挂载流程完成后：按当前 Enabled 决定是否挂上补丁。</summary>
        private static void OnPatched()
        {
            SelfHarmony ??= new Harmony("DT_Tools.DoorLockServer");
            if (Engine.Enabled<DoorLockServerFeature>())
                ApplyPatches();
            else
                RemovePatches();
        }

        private static void OnEnabled() => ApplyPatches();

        private static void OnDisabled() => RemovePatches();

        private static void ApplyPatches()
        {
            if (_patched) return;
            SelfHarmony ??= new Harmony("DT_Tools.DoorLockServer");
            SelfHarmony.PatchAll(typeof(DoorLockServerHandleEventPatch));
            _patched = true;
            Log.Info<DoorLockServerFeature>("补丁已挂载（放行前缀 + 冷却 Transpiler）");
        }

        private static void RemovePatches()
        {
            if (SelfHarmony == null) return;
            SelfHarmony.UnpatchSelf();
            _patched = false;
            Log.Info<DoorLockServerFeature>("补丁已卸载");
        }
    }
}
