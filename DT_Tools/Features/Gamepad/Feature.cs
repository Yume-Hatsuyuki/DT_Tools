using System;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;

namespace DT_Tools.Features.Gamepad
{
    [PatchFeature(
        "手柄：Windows XInput 手柄只控制游戏（WebUI 不响应）。\n左摇杆移动：按钮按映射输出游戏动作（读玩家实际键位）、固定键或鼠标左右键。\n光标模式（默认 Back 切换）：右摇杆移光标，A/B 点击，用于菜单；未单独设置的按钮跟随普通模式。\n光标模式下系统光标保持可见（游戏会在空闲 3 秒后隐藏光标）。\n映射在 WebUI「手柄」应用里配置。Steam Input 默认设置即可。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        Tags = FeatureTag.Peripheral,
        Author = "梦初雪")]
    public sealed class GamepadFeature
    {
        [Config("摇杆死区（0-0.5）：偏移小于该值视为静止。", Min = 0f, Max = 0.5f)]
        public static float DeadZone = 0.15f;

        [Config("扳机按下阈值（0.1-0.9）：扳机越过该值视为按下 LT/RT。", Min = 0.1f, Max = 0.9f)]
        public static float TriggerThreshold = 0.5f;

        [Config("光标模式下右摇杆满偏时每秒移动的像素（200-2500）。", Min = 200f, Max = 2500f)]
        public static float CursorSpeed = 900f;

        private static void OnPatched()
        {
            if (Engine.Enabled<GamepadFeature>())
                Start();
        }

        private static void OnEnabled() => Start();

        private static void OnDisabled() => Stop();

        private static void Start()
        {
            KeyBindings.OnChanged -= OnKeysChanged;
            KeyBindings.OnChanged += OnKeysChanged;
            GamepadService.SetFeatureEnabled(true);
            GamepadService.Start(Tuning, CoroutineHost.Post, ReadActionKeyOnMain);
        }

        private static void Stop()
        {
            KeyBindings.OnChanged -= OnKeysChanged;
            GamepadService.Stop();
            GamepadService.SetFeatureEnabled(false);
        }

        private static void OnKeysChanged() => GamepadService.MarkKeysDirty();

        private static GamepadTuning Tuning() => new GamepadTuning
        {
            DeadZone = DeadZone,
            TriggerThreshold = TriggerThreshold,
            CursorSpeed = CursorSpeed,
        };

        private static int ReadActionKeyOnMain(int index)
        {
            if (index < 0 || index >= GamepadActions.Count)
                return 0;
            if (!Enum.TryParse(GamepadActions.Names[index], out Define.EInputAction action))
                return 0;
            return UnityKeys.UnityToVk((int)KeyBindings.Get(action));
        }
    }
}
