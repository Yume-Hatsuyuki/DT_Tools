using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using HarmonyLib;

namespace DT_Tools.Features.DoorLockServer
{
    [PatchFeature(
        "锁门服务端：LockDoorMode 选额外放行谁（Dark=默认原版仅黑幕；Black=黑幕+黑方；White=黑幕+白方；All=全部）；锁门冷却间隔 Interval 可调（原版 40s，0=无冷却）。\n进别人房间时取决于房主是否启用本功能；客户端按钮显示见「SabotageButtonUnlock」。",
        defaultEnabled: false,
        side: FeatureSide.Host,
        Tags = FeatureTag.GameDoor,
        Author = "梦初雪")]
    public sealed class DoorLockServerFeature
    {
        [Config("锁门额外放行身份：Dark=默认（原版档位，仅黑幕）；Black=黑幕+黑方；White=黑幕+白方；All=全部。")]
        public static LockDoorMode Mode = LockDoorMode.Black;

        [Config("锁门冷却间隔（秒）：多久可以再次锁门（服务端判定，客户端倒计时自动跟随）。0=无冷却。原版 40。")]
        public static int Interval = 40;

        internal static Harmony SelfHarmony;

        private static bool _patched;

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
