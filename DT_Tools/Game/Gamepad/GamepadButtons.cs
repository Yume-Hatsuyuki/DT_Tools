using System.Collections.Generic;

namespace DT_Tools.Game
{
    internal static class GamepadButtons
    {
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

        public static Dictionary<string, int> IndexByName()
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < Names.Length; i++)
                map[Names[i]] = i;
            return map;
        }
    }
}
