namespace DT_Tools.Game
{
    internal static class GamepadActions
    {
        public static readonly string[] Names =
        {
            "MoveUp", "MoveDown", "MoveLeft", "MoveRight", "Interact", "Special", "UseItem", "Skill",
            "Tablet", "Run", "DropItem", "Emote", "PushTalk",
        };

        private static readonly string[] Labels =
        {
            "上移", "下移", "左移", "右移", "交互", "特殊", "使用道具", "技能",
            "平板", "奔跑", "丢弃道具", "表情", "按键说话",
        };

        public static int Count => Names.Length;

        public static string LabelOf(int index) => Labels[index];

        public static int IndexOf(string name)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (Names[i] == name)
                    return i;
            }
            return -1;
        }
    }
}
