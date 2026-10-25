using System;
using System.Collections.Generic;
using System.IO;
using DT_Tools.Core;

namespace DT_Tools.Game
{
    internal sealed class GamepadMapping
    {
        public const string LayerNormal = "normal";
        public const string LayerCursor = "cursor";
        public const string DefaultToggle = "Back";

        public const int FileVersion = 2;

        private const string Tag = "Gamepad";

        private readonly object _lock = new object();
        private readonly string[] _normal = new string[GamepadButtons.Count];
        private readonly string[] _cursor = new string[GamepadButtons.Count];
        private string _toggle = DefaultToggle;

        public GamepadMapping()
        {
            ResetInternal();
        }

        public static GamepadMapping Load(string path)
        {
            var mapping = new GamepadMapping();
            try
            {
                if (!File.Exists(path))
                    return mapping;
                string text = File.ReadAllText(path);
                if (!Json.TryFrom<MappingFile>(text, out var file) || file == null)
                {
                    Log.Warn(Tag, "手柄映射文件无法解析，已按默认映射处理");
                    return mapping;
                }
                mapping.ApplyFile(file);
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"手柄映射读取失败，已按默认映射处理: {ex.Message}");
            }
            return mapping;
        }

        public void Save(string path)
        {
            MappingFile file;
            lock (_lock)
            {
                file = new MappingFile
                {
                    Version = FileVersion,
                    Toggle = _toggle,
                    Normal = ToEntries(_normal),
                    Cursor = ToEntries(_cursor),
                };
            }
            try
            {
                DtPaths.EnsureParentOf(path);
                File.WriteAllText(path, Json.To(file));
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"手柄映射保存失败: {ex.Message}");
            }
        }

        public bool Bind(string layer, string button, string token, out string error)
        {
            error = null;
            if (!GamepadButtons.IndexByName().TryGetValue(button ?? "", out int index))
            {
                error = "按钮无效";
                return false;
            }
            bool inherit = token == GamepadOutput.InheritToken;
            if (inherit && layer != LayerCursor)
            {
                error = "只有光标模式可以跟随普通模式";
                return false;
            }
            if (!inherit && !GamepadOutput.IsValid(token))
            {
                error = "输出无效";
                return false;
            }
            lock (_lock)
            {
                if (layer == LayerNormal)
                    _normal[index] = token;
                else if (layer == LayerCursor)
                    _cursor[index] = inherit ? null : token;
                else
                {
                    error = "层无效";
                    return false;
                }
            }
            return true;
        }

        public bool SetToggle(string button, out string error)
        {
            error = null;
            if (!GamepadButtons.TryParse(button ?? "", out _))
            {
                error = "按钮无效";
                return false;
            }
            lock (_lock)
                _toggle = button;
            return true;
        }

        public void Reset()
        {
            lock (_lock)
                ResetInternal();
        }

        public string[] Tokens(string layer)
        {
            lock (_lock)
                return layer == LayerCursor ? EffectiveCursor() : (string[])_normal.Clone();
        }

        public bool[] CursorInherited()
        {
            lock (_lock)
            {
                var result = new bool[_cursor.Length];
                for (int i = 0; i < result.Length; i++)
                    result[i] = _cursor[i] == null;
                return result;
            }
        }

        public string Toggle
        {
            get
            {
                lock (_lock)
                    return _toggle;
            }
        }

        public GamepadProfile Compile()
        {
            lock (_lock)
            {
                CompileLayer(_normal, out var nk, out var na);
                CompileLayer(EffectiveCursor(), out var ck, out var ca);
                GamepadButtons.TryParse(_toggle, out GamepadButton toggle);
                return new GamepadProfile(nk, na, ck, ca, toggle);
            }
        }

        private static void CompileLayer(string[] tokens, out GamepadOutputKind[] kinds, out int[] args)
        {
            kinds = new GamepadOutputKind[tokens.Length];
            args = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (GamepadOutput.TryParse(tokens[i], out var kind, out int arg))
                {
                    kinds[i] = kind;
                    args[i] = arg;
                }
                else
                {
                    kinds[i] = GamepadOutputKind.None;
                    args[i] = 0;
                }
            }
        }

        private string[] EffectiveCursor()
        {
            var result = new string[_cursor.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = _cursor[i] ?? _normal[i];
            return result;
        }

        private void ApplyFile(MappingFile file)
        {
            lock (_lock)
            {
                ResetInternal();
                bool legacy = file.Version < FileVersion;
                ApplyLayer(file.Normal, _normal, LayerNormal, false, false);
                ApplyLayer(file.Cursor, _cursor, LayerCursor, true, legacy);
                if (!string.IsNullOrEmpty(file.Toggle) && GamepadButtons.TryParse(file.Toggle, out _))
                    _toggle = file.Toggle;
            }
        }

        private static void ApplyLayer(List<BindingEntry> source, string[] target, string layerName, bool allowInherit, bool legacyNoneIsInherit)
        {
            if (source == null)
                return;
            var index = GamepadButtons.IndexByName();
            foreach (BindingEntry entry in source)
            {
                if (entry == null || !index.TryGetValue(entry.Button ?? "", out int i))
                    continue;
                bool inherit = allowInherit
                    && (entry.Output == GamepadOutput.InheritToken
                        || (legacyNoneIsInherit && entry.Output == GamepadOutput.NoneToken));
                if (inherit)
                {
                    target[i] = null;
                    continue;
                }
                if (GamepadOutput.IsValid(entry.Output))
                    target[i] = entry.Output;
                else
                    Log.Warn(Tag, $"手柄映射 {layerName}.{entry.Button} 的输出 \"{entry.Output}\" 无效，已用默认值");
            }
        }

        private void ResetInternal()
        {
            FillDefaults(_normal, DefaultNormal);
            FillCursorDefaults();
            _toggle = DefaultToggle;
        }

        private static void FillDefaults(string[] target, Dictionary<string, string> defaults)
        {
            for (int i = 0; i < target.Length; i++)
            {
                string name = GamepadButtons.NameOf(i);
                target[i] = defaults.TryGetValue(name, out string token) ? token : GamepadOutput.NoneToken;
            }
        }

        private void FillCursorDefaults()
        {
            for (int i = 0; i < _cursor.Length; i++)
            {
                string name = GamepadButtons.NameOf(i);
                _cursor[i] = DefaultCursor.TryGetValue(name, out string token) ? token : null;
            }
        }

        private static List<BindingEntry> ToEntries(string[] tokens)
        {
            var list = new List<BindingEntry>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
                list.Add(new BindingEntry { Button = GamepadButtons.NameOf(i), Output = tokens[i] ?? GamepadOutput.InheritToken });
            return list;
        }

        private static readonly Dictionary<string, string> DefaultNormal = new Dictionary<string, string>
        {
            { "A", GamepadOutput.ActionToken("Interact") },
            { "B", GamepadOutput.ActionToken("Special") },
            { "X", GamepadOutput.ActionToken("UseItem") },
            { "Y", GamepadOutput.ActionToken("Tablet") },
            { "LB", GamepadOutput.ActionToken("DropItem") },
            { "RB", GamepadOutput.ActionToken("Skill") },
            { "LT", GamepadOutput.MouseRightToken },
            { "RT", GamepadOutput.MouseLeftToken },
            { "LS", GamepadOutput.ActionToken("Run") },
            { "RS", GamepadOutput.ActionToken("Emote") },
            { "Start", GamepadOutput.KeyToken("Escape") },
            { "DUp", GamepadOutput.KeyToken("UpArrow") },
            { "DDown", GamepadOutput.KeyToken("DownArrow") },
            { "DLeft", GamepadOutput.KeyToken("LeftArrow") },
            { "DRight", GamepadOutput.KeyToken("RightArrow") },
        };

        private static readonly Dictionary<string, string> DefaultCursor = new Dictionary<string, string>
        {
            { "A", GamepadOutput.MouseLeftToken },
            { "B", GamepadOutput.MouseRightToken },
            { "Start", GamepadOutput.KeyToken("Escape") },
            { "DUp", GamepadOutput.KeyToken("UpArrow") },
            { "DDown", GamepadOutput.KeyToken("DownArrow") },
            { "DLeft", GamepadOutput.KeyToken("LeftArrow") },
            { "DRight", GamepadOutput.KeyToken("RightArrow") },
        };

        private sealed class MappingFile
        {
            public int Version = 1;
            public string Toggle = DefaultToggle;
            public List<BindingEntry> Normal = new List<BindingEntry>();
            public List<BindingEntry> Cursor = new List<BindingEntry>();
        }

        private sealed class BindingEntry
        {
            public string Button = "";
            public string Output = "";
        }
    }
}
