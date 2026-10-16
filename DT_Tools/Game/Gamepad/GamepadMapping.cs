using System;
using System.Collections.Generic;
using System.IO;
using DT_Tools.Core;

namespace DT_Tools.Game
{
    /// <summary>
    /// 手柄映射表（可变，线程安全）：普通与光标两层的按钮输出，以及切换光标模式的按钮。
    /// </summary>
    /// <remarks>
    /// 持久化到 DtPaths.GamepadMapping（JSON，非 [Config]：结构是按钮表，不是段/键）。
    /// 光标层的空值（null）表示「跟随普通层」：玩家没有为该按钮单独设置光标输出。
    /// 显示（Tokens）与运行时（Compile）都取光标层的生效值，即光标层为空时使用普通层的输出，因此不会把未设置显示成「无」。
    /// 读取时逐项校验：非法 token 退回默认并告警，不让一个坏值废掉整张表。
    /// </remarks>
    internal sealed class GamepadMapping
    {
        public const string LayerNormal = "normal";
        public const string LayerCursor = "cursor";
        public const string DefaultToggle = "Back";

        /// <summary>
        /// 映射文件版本。v2 起光标层用 inherit 表示跟随；v1 保存时把未设置的光标项写成 none，读取时迁移为跟随。
        /// </summary>
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

        /// <summary>
        /// 从文件读取映射；文件缺失或无法解析时返回默认映射。
        /// </summary>
        /// <param name="path">映射文件路径。</param>
        /// <returns>映射表。</returns>
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

        /// <summary>
        /// 保存到文件。
        /// </summary>
        /// <param name="path">映射文件路径。</param>
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

        /// <summary>
        /// 设置某层某按钮的输出。光标层可传 inherit，恢复为跟随普通层。
        /// </summary>
        /// <param name="layer">层：normal 或 cursor。</param>
        /// <param name="button">按钮名称，如 "A"。</param>
        /// <param name="token">输出 token，如 "action:Interact"；仅光标层接受 "inherit"。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>设置成功时为 true。</returns>
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

        /// <summary>
        /// 设置切换光标模式的按钮。
        /// </summary>
        /// <param name="button">按钮名称。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>设置成功时为 true。</returns>
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

        /// <summary>
        /// 恢复默认映射。
        /// </summary>
        public void Reset()
        {
            lock (_lock)
                ResetInternal();
        }

        /// <summary>
        /// 读取某层的生效输出（按 GamepadButtons 顺序）。光标层未单独设置的按钮返回普通层的输出。
        /// </summary>
        /// <param name="layer">层。</param>
        /// <returns>token 数组副本。</returns>
        public string[] Tokens(string layer)
        {
            lock (_lock)
                return layer == LayerCursor ? EffectiveCursor() : (string[])_normal.Clone();
        }

        /// <summary>
        /// 光标层各按钮是否跟随普通层（即玩家未单独设置）。
        /// </summary>
        /// <returns>与 GamepadButtons.All 同序的数组副本；true 表示跟随。</returns>
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

        /// <summary>
        /// 切换光标模式的按钮名称。
        /// </summary>
        public string Toggle
        {
            get
            {
                lock (_lock)
                    return _toggle;
            }
        }

        /// <summary>
        /// 编译为运行时使用的不可变快照。
        /// </summary>
        /// <returns>映射快照。</returns>
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

        /// <summary>
        /// 光标层生效值：未单独设置的按钮取普通层的输出。调用方须持有 _lock。
        /// </summary>
        /// <returns>与 GamepadButtons.All 同序的 token 数组（新数组）。</returns>
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

        /// <summary>
        /// 把文件中的一层写入目标数组。
        /// </summary>
        /// <param name="source">文件中的一层条目；为 null 时不做任何改动。</param>
        /// <param name="target">目标数组。光标层中 null 表示跟随普通层。</param>
        /// <param name="layerName">层名，仅用于日志。</param>
        /// <param name="allowInherit">是否允许 inherit（只有光标层为 true）。</param>
        /// <param name="legacyNoneIsInherit">v1 文件：none 实为未设置，迁移为跟随。</param>
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

        /// <summary>
        /// 光标层默认：有预设的按钮写预设输出，其余置空，表示跟随普通层。
        /// </summary>
        private void FillCursorDefaults()
        {
            for (int i = 0; i < _cursor.Length; i++)
            {
                string name = GamepadButtons.NameOf(i);
                _cursor[i] = DefaultCursor.TryGetValue(name, out string token) ? token : null;
            }
        }

        /// <summary>
        /// 按钮表转为文件条目。空值写成 inherit，使「跟随」能被读回。
        /// </summary>
        /// <param name="tokens">与 GamepadButtons.All 同序的 token 数组。</param>
        /// <returns>文件条目列表。</returns>
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

        /// <summary>
        /// 光标层只预设菜单必需的输出；未列出的按钮跟随普通层。
        /// </summary>
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

        /// <summary>
        /// 文件结构。层用数组而不是字典：序列化器会把字典键转成小写（"A" → "a"），读回会对不上。
        /// </summary>
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
