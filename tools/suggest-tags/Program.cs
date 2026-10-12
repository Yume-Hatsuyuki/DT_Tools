using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SuggestTags
{
    /// 标签建议工具：语义分析功能代码实际引用了哪些游戏类型/成员，按规则表映射到标签。
    /// 只给建议，不改源码。同一输入输出确定（CI 可重复）。
    internal static class Program
    {
        private const string GameAssembly = "Assembly-CSharp";
        private static readonly string[] GameAssemblies = { "Assembly-CSharp", "Assembly-CSharp-firstpass" };

        private static int Main(string[] args)
        {
            var opt = Options.Parse(args);
            if (opt == null) { Console.Error.WriteLine(Options.Usage); return 2; }

            var projectDir = Path.Combine(opt.Root, "DT_Tools");
            var csproj = Path.Combine(projectDir, "DT_Tools.csproj");
            if (!File.Exists(csproj)) { Console.Error.WriteLine("找不到 " + csproj); return 2; }
            var assets = Path.Combine(projectDir, "obj", "project.assets.json");
            if (!File.Exists(assets)) { Console.Error.WriteLine("缺少 obj/project.assets.json，请先 dotnet restore。"); return 2; }

            var map = GameMap.Load(Path.Combine(opt.Root, "tools", "suggest-tags", "game-map.json"));
            var refs = BuildReferences(opt.Root, assets);

            var trees = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                .Where(p => !IsBuildDir(p, projectDir))
                .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p, Encoding.UTF8), new CSharpParseOptions(LanguageVersion.Latest), path: p))
                .ToList();
            var comp = CSharpCompilation.Create("DTToolsAnalysis", trees, refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

            var units = DiscoverUnits(comp, trees, projectDir);
            var evidence = new Dictionary<UnitInfo, UnitEvidence>();
            foreach (var u in units) evidence[u] = Collect(comp, u, projectDir);

            var result = Scorer.Score(units, evidence, map, opt.Threshold);
            Report.Print(result, opt);
            return 0;
        }

        private static bool IsBuildDir(string path, string projectDir)
        {
            var rel = path.Substring(projectDir.Length).Replace('\\', '/');
            return rel.StartsWith("/obj/") || rel.StartsWith("/bin/");
        }

        /// 编译引用 = project.assets.json 的 netstandard2.1 编译资产 + libs/（游戏程序集）+ 运行时 TPA（仅补基础库）。
        private static List<MetadataReference> BuildReferences(string root, string assetsPath)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<MetadataReference>();
            void Add(string path)
            {
                var name = Path.GetFileName(path);
                if (!File.Exists(path) || !seen.Add(name)) return;
                list.Add(MetadataReference.CreateFromFile(path));
            }
            foreach (var dll in Directory.EnumerateFiles(Path.Combine(root, "libs"), "*.dll")) Add(dll);

            using (var doc = JsonDocument.Parse(File.ReadAllText(assetsPath)))
            {
                var r = doc.RootElement;
                var folders = r.GetProperty("packageFolders").EnumerateObject().Select(p => p.Name).ToList();
                var targets = r.GetProperty("targets");
                JsonElement target = default;
                foreach (var t in targets.EnumerateObject())
                    if (t.Name.StartsWith(".NETStandard,Version=v2.1", StringComparison.Ordinal)) { target = t.Value; break; }
                if (target.ValueKind != JsonValueKind.Object) target = targets.EnumerateObject().First().Value;
                var libraries = r.GetProperty("libraries");
                foreach (var lib in target.EnumerateObject())
                {
                    if (!libraries.TryGetProperty(lib.Name, out var meta)) continue;
                    if (!lib.Value.TryGetProperty("compile", out var compile)) continue;
                    var rel = meta.GetProperty("path").GetString();
                    foreach (var asset in compile.EnumerateObject())
                    {
                        var file = Path.GetFileName(asset.Name);
                        foreach (var f in folders)
                            Add(Path.Combine(f, rel, asset.Name.Replace('/', Path.DirectorySeparatorChar)));
                    }
                }
            }

            var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(tpa))
                foreach (var p in tpa.Split(Path.PathSeparator)) Add(p);
            return list;
        }

        /// 找出所有功能 / 自动化模块 / MCP 工具 / 命令；记下它们的声明标签与所在目录。
        private static List<UnitInfo> DiscoverUnits(CSharpCompilation comp, List<SyntaxTree> trees, string projectDir)
        {
            var units = new List<UnitInfo>();
            foreach (var tree in trees)
            {
                var model = comp.GetSemanticModel(tree);
                foreach (var cls in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var sym = model.GetDeclaredSymbol(cls);
                    if (sym == null) continue;
                    // partial 类的非首段没有特性语法：只把带特性（或声明了 ICommand）的那一段当作单元，避免重复计数
                    bool hasUnitSyntax = cls.AttributeLists.Count > 0 || (cls.BaseList != null && cls.BaseList.Types.Any(t => t.ToString().Contains("ICommand")));
                    if (!hasUnitSyntax) continue;
                    string kind = null;
                    var attrs = sym.GetAttributes().Select(a => a.AttributeClass?.Name).ToList();
                    if (attrs.Contains("PatchFeatureAttribute")) kind = "功能";
                    else if (attrs.Contains("AutomationModuleAttribute")) kind = "模块";
                    else if (attrs.Contains("McpToolAttribute")) kind = "MCP";
                    else if (sym.AllInterfaces.Any(i => i.Name == "ICommand")) kind = "命令";
                    if (kind == null) continue;

                    var declared = ReadDeclaredTags(cls);
                    var relFile = Rel(tree.FilePath, projectDir);
                    var folder = Path.GetDirectoryName(relFile).Replace('\\', '/');
                    var name = kind == "命令" ? relFile.Replace(".cs", "") + "::" + cls.Identifier.Text : folder;
                    units.Add(new UnitInfo { Kind = kind, Name = name, Folder = folder, Declared = declared, File = relFile, ClassName = cls.Identifier.Text });
                }
            }
            return units;
        }

        private static HashSet<string> ReadDeclaredTags(ClassDeclarationSyntax cls)
        {
            var text = new StringBuilder();
            foreach (var al in cls.AttributeLists)
                foreach (var a in al.Attributes)
                {
                    var n = a.Name.ToString();
                    if (!(n.Contains("PatchFeature") || n.Contains("AutomationModule") || n.Contains("McpTool"))) continue;
                    if (a.ArgumentList == null) continue;
                    foreach (var arg in a.ArgumentList.Arguments)
                        if (arg.NameEquals != null && arg.NameEquals.Name.Identifier.Text == "Tags")
                            text.Append(arg.Expression.ToString());
                }
            foreach (var prop in cls.Members.OfType<PropertyDeclarationSyntax>())
                if (prop.Identifier.Text == "Tags" && prop.ExpressionBody != null)
                    text.Append(prop.ExpressionBody.Expression.ToString());
            return new HashSet<string>(Regex.Matches(text.ToString(), @"FeatureTag\.(\w+)").Cast<Match>().Select(m => m.Groups[1].Value));
        }

        /// 单元引用的游戏类型与成员（计数），以及每个键的首次出现位置（证据）。
        private static UnitEvidence Collect(CSharpCompilation comp, UnitInfo unit, string projectDir)
        {
            var ev = new UnitEvidence();
            foreach (var tree in comp.SyntaxTrees)
            {
                var rel = Rel(tree.FilePath, projectDir);
                bool inUnit = unit.Kind == "命令" ? Path.GetDirectoryName(rel).Replace('\\', '/') == unit.Folder
                                                  : rel.StartsWith(unit.Folder + "/", StringComparison.Ordinal);
                if (!inUnit) continue;
                var model = comp.GetSemanticModel(tree);
                foreach (var id in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var sym = model.GetSymbolInfo(id).Symbol;
                    if (sym == null) continue;
                    var owner = (sym as ITypeSymbol) ?? sym.ContainingType;
                    if (owner == null) continue;
                    owner = owner.OriginalDefinition;
                    if (!IsGame(owner)) continue;
                    var typeKey = Qualified(owner);
                    ev.Types[typeKey] = ev.Types.TryGetValue(typeKey, out var c) ? c + 1 : 1;
                    if (!ev.Where.ContainsKey(typeKey)) ev.Where[typeKey] = rel + ":" + (tree.GetLineSpan(id.Span).StartLinePosition.Line + 1);
                    if (!(sym is ITypeSymbol) && sym.ContainingType != null)
                    {
                        var memberKey = typeKey + "." + sym.Name;
                        ev.Members[memberKey] = ev.Members.TryGetValue(memberKey, out var m) ? m + 1 : 1;
                    }
                }
            }
            return ev;
        }

        private static bool IsGame(ISymbol owner)
        {
            var asm = owner.ContainingAssembly?.Name;
            if (asm != null && GameAssemblies.Contains(asm)) return true;
            var ns = owner.ContainingNamespace?.ToDisplayString() ?? "";
            return ns.StartsWith("DT_Tools.Game", StringComparison.Ordinal);   // 模组里的共享游戏助手
        }

        private static string Qualified(ISymbol s)
        {
            var ns = s.ContainingNamespace;
            return (ns == null || ns.IsGlobalNamespace) ? s.Name : ns.ToDisplayString() + "." + s.Name;
        }

        private static string Rel(string path, string projectDir)
            => path.Substring(projectDir.Length).TrimStart('/', '\\').Replace('\\', '/');
    }

    internal sealed class Options
    {
        public string Root = Directory.GetCurrentDirectory();
        public string Unit;
        public bool All;
        public bool Check;
        public bool Json;
        public double Threshold = 0.4;

        public const string Usage =
@"用法: suggest-tags [--root <仓库根>] (--all | --unit <目录或 类名>) [--check] [--json] [--threshold 0.4]
  --all        对全部单元给出建议
  --unit       只看一个单元（功能目录名，或命令的 Commands/目录::类名）
  --check      与声明的 Tags 对比，输出差异与一致性统计
  --json       以 JSON 输出
  --threshold  相对最高分的截断比例，默认 0.4";

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root": o.Root = args[++i]; break;
                    case "--unit": o.Unit = args[++i]; break;
                    case "--all": o.All = true; break;
                    case "--check": o.Check = true; break;
                    case "--json": o.Json = true; break;
                    case "--threshold": o.Threshold = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    default: return null;
                }
            }
            if (!o.All && o.Unit == null && !o.Check) return null;
            return o;
        }
    }

    internal sealed class UnitInfo
    {
        public string Kind;
        public string Name;
        public string Folder;
        public string File;
        public string ClassName;
        public HashSet<string> Declared;
    }

    internal sealed class UnitEvidence
    {
        public Dictionary<string, int> Types = new Dictionary<string, int>();
        public Dictionary<string, int> Members = new Dictionary<string, int>();
        public Dictionary<string, string> Where = new Dictionary<string, string>();
    }

    internal sealed class Rule
    {
        public Regex Pattern;
        public string[] Tags;
        public double Weight;
    }

    internal sealed class GameMap
    {
        public List<Rule> Rules = new List<Rule>();
        public Dictionary<string, string[]> Members = new Dictionary<string, string[]>();

        public static GameMap Load(string path)
        {
            var map = new GameMap();
            if (!File.Exists(path)) return map;
            using (var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8)))
            {
                var r = doc.RootElement;
                if (r.TryGetProperty("rules", out var rules))
                    foreach (var e in rules.EnumerateArray())
                        map.Rules.Add(new Rule
                        {
                            Pattern = new Regex(e.GetProperty("pattern").GetString()),
                            Tags = e.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToArray(),
                            Weight = e.GetProperty("weight").GetDouble(),
                        });
                if (r.TryGetProperty("members", out var members))
                    foreach (var p in members.EnumerateObject())
                        map.Members[p.Name] = p.Value.EnumerateArray().Select(t => t.GetString()).ToArray();
            }
            return map;
        }
    }

    internal sealed class Suggestion
    {
        public string Tag;
        public double Score;
        public List<string> Evidence = new List<string>();
    }

    internal sealed class UnitResult
    {
        public UnitInfo Unit;
        public List<Suggestion> Suggested = new List<Suggestion>();
        public Dictionary<string, double> AllScores = new Dictionary<string, double>();
    }

    internal static class Scorer
    {
        /// 思路：每个游戏类型的权重 = ln(1 + 单元总数 / 引用它的单元数)，越普遍（枢纽类）越低。
        /// 类型被规则命中时，把规则权重累加到对应标签上；成员的精确映射权重更高。
        public static List<UnitResult> Score(List<UnitInfo> units, Dictionary<UnitInfo, UnitEvidence> ev, GameMap map, double threshold)
        {
            int n = units.Count;
            var typeDf = new Dictionary<string, int>();
            var memberDf = new Dictionary<string, int>();
            foreach (var u in units)
            {
                foreach (var k in ev[u].Types.Keys) typeDf[k] = typeDf.TryGetValue(k, out var c) ? c + 1 : 1;
                foreach (var k in ev[u].Members.Keys) memberDf[k] = memberDf.TryGetValue(k, out var c) ? c + 1 : 1;
            }

            var results = new List<UnitResult>();
            foreach (var u in units)
            {
                var r = new UnitResult { Unit = u };
                var scores = new Dictionary<string, double>();
                var contrib = new Dictionary<string, List<(string key, double s)>>();
                void Add(string tag, string key, double s)
                {
                    scores[tag] = scores.TryGetValue(tag, out var v) ? v + s : s;
                    if (!contrib.ContainsKey(tag)) contrib[tag] = new List<(string, double)>();
                    contrib[tag].Add((key, s));
                }

                foreach (var kv in ev[u].Types)
                {
                    var idf = Math.Log(1 + (double)n / typeDf[kv.Key]);
                    var mult = Math.Min(2.0, 1 + Math.Log(kv.Value)) * (kv.Key.StartsWith("Protocol.", StringComparison.Ordinal) ? 0.4 : 1.0);
                    var simple = kv.Key.Substring(kv.Key.LastIndexOf('.') + 1);
                    var best = new Dictionary<string, double>();
                    foreach (var rule in map.Rules)
                    {
                        if (!rule.Pattern.IsMatch(simple)) continue;
                        foreach (var t in rule.Tags)
                            best[t] = Math.Max(best.TryGetValue(t, out var b) ? b : 0, rule.Weight);
                    }
                    foreach (var kv2 in best) Add(kv2.Key, kv.Key, idf * mult * kv2.Value);
                }
                foreach (var kv in ev[u].Members)
                {
                    if (!map.Members.TryGetValue(kv.Key, out var tags)) continue;
                    var idf = Math.Log(1 + (double)n / memberDf[kv.Key]);
                    foreach (var t in tags) Add(t, kv.Key, idf * 1.5);
                }

                r.AllScores = scores;
                if (scores.Count > 0)
                {
                    var max = scores.Values.Max();
                    var ranked = scores.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
                    foreach (var p in ranked.Where(p => p.Value >= threshold * max).Take(3))
                    {
                        var sg = new Suggestion { Tag = p.Key, Score = p.Value };
                        sg.Evidence = contrib[p.Key].OrderByDescending(x => x.s).Take(3)
                            .Select(x => x.key + (ev[u].Where.TryGetValue(x.key, out var w) ? " @" + w : "")).ToList();
                        r.Suggested.Add(sg);
                    }
                }
                results.Add(r);
            }
            return results;
        }
    }

    internal static class Report
    {
        public static void Print(List<UnitResult> results, Options opt)
        {
            var selected = results.Where(r => opt.All || opt.Check || r.Unit.Name.Contains(opt.Unit) || r.Unit.ClassName == opt.Unit).ToList();
            if (opt.Json) { PrintJson(selected); return; }
            if (opt.Check) { PrintCheck(results); return; }
            foreach (var r in selected) PrintOne(r);
        }

        private static void PrintOne(UnitResult r)
        {
            var declared = string.Join(",", r.Unit.Declared.OrderBy(x => x, StringComparer.Ordinal));
            Console.WriteLine($"[{r.Unit.Kind}] {r.Unit.Name}");
            Console.WriteLine($"  声明: {(declared.Length == 0 ? "（无）" : declared)}");
            if (r.Suggested.Count == 0) Console.WriteLine("  建议: （无可用证据）");
            foreach (var s in r.Suggested)
                Console.WriteLine($"  建议: {s.Tag,-16} 分={s.Score:0.00}  证据: {string.Join("；", s.Evidence)}");
        }

        private static void PrintCheck(List<UnitResult> results)
        {
            int withDeclared = 0, top1Hit = 0, anyHit = 0;
            double precisionSum = 0, recallSum = 0;
            foreach (var r in results)
            {
                var d = r.Unit.Declared;
                var s = r.Suggested.Select(x => x.Tag).ToList();
                var inter = s.Where(d.Contains).ToList();
                if (d.Count > 0)
                {
                    withDeclared++;
                    if (s.Count > 0 && d.Contains(s[0])) top1Hit++;
                    if (inter.Count > 0) anyHit++;
                    precisionSum += s.Count == 0 ? 0 : (double)inter.Count / s.Count;
                    recallSum += (double)inter.Count / d.Count;
                }
                var missingEvidence = d.Where(t => !r.AllScores.ContainsKey(t)).ToList();
                var strongUndeclared = r.Suggested.Where(x => !d.Contains(x.Tag)).Select(x => x.Tag).ToList();
                if (missingEvidence.Count == 0 && strongUndeclared.Count == 0) continue;
                Console.WriteLine($"[{r.Unit.Kind}] {r.Unit.Name}");
                if (missingEvidence.Count > 0) Console.WriteLine("  ✗ 声明了但代码里找不到证据: " + string.Join(",", missingEvidence));
                if (strongUndeclared.Count > 0) Console.WriteLine("  ⚠ 强信号但未声明: " + string.Join(",", strongUndeclared));
            }
            Console.WriteLine();
            Console.WriteLine($"单元 {results.Count}，其中有声明的 {withDeclared}");
            if (withDeclared > 0)
            {
                Console.WriteLine($"  建议的第一名与声明一致: {top1Hit}/{withDeclared} ({100.0 * top1Hit / withDeclared:0}%)");
                Console.WriteLine($"  建议（前三）与声明至少一个一致: {anyHit}/{withDeclared} ({100.0 * anyHit / withDeclared:0}%)");
                Console.WriteLine($"  平均精确率（建议中正确的比例）: {100 * precisionSum / withDeclared:0}%");
                Console.WriteLine($"  平均召回率（声明中被建议到的比例）: {100 * recallSum / withDeclared:0}%");
            }
        }

        private static void PrintJson(List<UnitResult> results)
        {
            var list = results.Select(r => new
            {
                kind = r.Unit.Kind,
                name = r.Unit.Name,
                declared = r.Unit.Declared.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                suggested = r.Suggested.Select(s => new { tag = s.Tag, score = Math.Round(s.Score, 3), evidence = s.Evidence }).ToArray(),
            });
            Console.WriteLine(JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
