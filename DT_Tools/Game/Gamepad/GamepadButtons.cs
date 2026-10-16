using System.Collections.Generic;

namespace DT_Tools.Game
{
    /// <summary>
    /// 手柄按钮的固定顺序、JSON 名称与中文标签。JSON 与 WebUI 都用这里的名称。
    /// </summary>
    internal static class GamepadButtons
    {
        /// <summary>
        /// 16 个可绑定按钮，顺序即映射表的下标顺序（不可随意调整）。
        /// </summary>
        public static readonly GamepadButton[] All =
        {
            GamepadButton.A, GamepadButton.B, GamepadButton.X, GamepadButton.Y,
            GamepadButton.LeftBumper, GamepadButton.RightBumper,
            GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
            GamepadButton.LeftStick, GamepadButton.RightStick,
            GamepadButton.Start, GamepadButton.Back,
            GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight,
        };

        private static readonly string[] Names =
        {
            "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS", "Start", "Back", "DUp", "DDown", "DLeft", "DRight",
        };

        private static readonly string[] Labels =
        {
            "A", "B", "X", "Y", "LB", "RB", "LT（扳机）", "RT（扳机）", "左摇杆按下", "右摇杆按下",
            "Start", "Back / 视图", "十字键上", "十字键下", "十字键左", "十字键右",
        };

        public static int Count => All.Length;

        /// <summary>
        /// 按钮在映射表中的下标。
        /// </summary>
        /// <param name="button">单个按钮。</param>
        /// <returns>下标；不是可绑定按钮时为 -1。</returns>
        public static int IndexOf(GamepadButton button)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == button)
                    return i;
            }
            return -1;
        }

        public static string NameOf(int index) => Names[index];

        public static string LabelOf(int index) => Labels[index];

        /// <summary>
        /// 由 JSON 名称解析按钮。
        /// </summary>
        /// <param name="name">按钮名称，例如 "A"、"LB"。</param>
        /// <param name="button">解析结果。</param>
        /// <returns>名称有效时为 true。</returns>
        public static bool TryParse(string name, out GamepadButton button)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (Names[i] == name)
                {
                    button = All[i];
                    return true;
                }
            }
            button = GamepadButton.None;
            return false;
        }

        /// <summary>
        /// 按钮名称到下标的字典，供校验与查找。
        /// </summary>
        /// <returns>名称 → 下标。</returns>
        public static Dictionary<string, int> IndexByName()
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < Names.Length; i++)
                map[Names[i]] = i;
            return map;
        }
    }
}
