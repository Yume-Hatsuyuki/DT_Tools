using System.Collections.Generic;

namespace DT_Tools.Game
{
    internal static class UnityKeys
    {
        private struct Entry
        {
            public readonly string Name;
            public readonly int UnityCode;
            public readonly int Vk;
            public readonly bool Extended;

            public Entry(string name, int unityCode, int vk, bool extended = false)
            {
                Name = name;
                UnityCode = unityCode;
                Vk = vk;
                Extended = extended;
            }
        }

        private static readonly List<Entry> Table = BuildTable();
        private static readonly Dictionary<string, int> VkByName = new Dictionary<string, int>();
        private static readonly Dictionary<int, int> VkByUnity = new Dictionary<int, int>();
        private static readonly HashSet<int> ExtendedVk = new HashSet<int>();

        static UnityKeys()
        {
            foreach (var e in Table)
            {
                VkByName[e.Name] = e.Vk;
                VkByUnity[e.UnityCode] = e.Vk;
                if (e.Extended)
                    ExtendedVk.Add(e.Vk);
            }
        }

        public static IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string>(Table.Count);
                foreach (var e in Table)
                    names.Add(e.Name);
                return names;
            }
        }

        public static bool TryNameToVk(string name, out int vk)
        {
            return VkByName.TryGetValue(name, out vk);
        }

        public static int UnityToVk(int unityKeyCode)
        {
            return VkByUnity.TryGetValue(unityKeyCode, out int vk) ? vk : 0;
        }

        public static bool IsExtended(int vk) => ExtendedVk.Contains(vk);

        private static List<Entry> BuildTable()
        {
            var t = new List<Entry>();
            for (int i = 0; i < 26; i++)
                t.Add(new Entry(((char)('A' + i)).ToString(), 97 + i, 0x41 + i));
            for (int i = 0; i < 10; i++)
                t.Add(new Entry("Alpha" + i, 48 + i, 0x30 + i));
            for (int i = 0; i < 10; i++)
                t.Add(new Entry("Keypad" + i, 256 + i, 0x60 + i));
            for (int i = 1; i <= 12; i++)
                t.Add(new Entry("F" + i, 281 + i, 0x6F + i));
            t.Add(new Entry("Space", 32, 0x20));
            t.Add(new Entry("Return", 13, 0x0D));
            t.Add(new Entry("Escape", 27, 0x1B));
            t.Add(new Entry("Tab", 9, 0x09));
            t.Add(new Entry("Backspace", 8, 0x08));
            t.Add(new Entry("CapsLock", 301, 0x14));
            t.Add(new Entry("UpArrow", 273, 0x26, true));
            t.Add(new Entry("DownArrow", 274, 0x28, true));
            t.Add(new Entry("RightArrow", 275, 0x27, true));
            t.Add(new Entry("LeftArrow", 276, 0x25, true));
            t.Add(new Entry("Insert", 277, 0x2D, true));
            t.Add(new Entry("Home", 278, 0x24, true));
            t.Add(new Entry("End", 279, 0x23, true));
            t.Add(new Entry("PageUp", 280, 0x21, true));
            t.Add(new Entry("PageDown", 281, 0x22, true));
            t.Add(new Entry("Delete", 127, 0x2E, true));
            t.Add(new Entry("LeftShift", 304, 0xA0));
            t.Add(new Entry("RightShift", 303, 0xA1));
            t.Add(new Entry("LeftControl", 306, 0xA2));
            t.Add(new Entry("RightControl", 305, 0xA3, true));
            t.Add(new Entry("LeftAlt", 308, 0xA4));
            t.Add(new Entry("RightAlt", 307, 0xA5, true));
            t.Add(new Entry("Minus", 45, 0xBD));
            t.Add(new Entry("Equals", 61, 0xBB));
            t.Add(new Entry("LeftBracket", 91, 0xDB));
            t.Add(new Entry("RightBracket", 93, 0xDD));
            t.Add(new Entry("Backslash", 92, 0xDC));
            t.Add(new Entry("Semicolon", 59, 0xBA));
            t.Add(new Entry("Quote", 39, 0xDE));
            t.Add(new Entry("Comma", 44, 0xBC));
            t.Add(new Entry("Period", 46, 0xBE));
            t.Add(new Entry("Slash", 47, 0xBF));
            t.Add(new Entry("BackQuote", 96, 0xC0));
            return t;
        }
    }
}
