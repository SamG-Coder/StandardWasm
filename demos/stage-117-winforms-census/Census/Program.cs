using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using WinFormsCensus;

// Usage: Census <app.dll> <desktop framework dir> <shared framework dir> <output dir> [--limit N] [--parallel N] [--no-compile]
if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: Census <app.dll> <desktop dir> <shared dir> <output dir> [--limit N] [--parallel N] [--no-compile]");
    return 2;
}
string appPath = Path.GetFullPath(args[0]), desktop = args[1], shared = args[2], output = args[3];
int limit = int.MaxValue, parallel = Math.Max(1, Environment.ProcessorCount / 2);
bool compile = true;
string? reuse = null;
for (int i = 4; i < args.Length; i++)
{
    if (args[i] == "--reuse") { reuse = args[++i]; continue; }
    if (args[i] == "--limit") limit = int.Parse(args[++i]);
    else if (args[i] == "--parallel") parallel = int.Parse(args[++i]);
    else if (args[i] == "--no-compile") compile = false;
}
Directory.CreateDirectory(output);

var set = new AssemblySet(Path.GetDirectoryName(appPath)!, desktop, shared);
LoadedAssembly app = set.Load(appPath);
int entryToken = app.Pe.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress;
var entry = new MethodId(app.Index, MetadataTokens.GetRowNumber(MetadataTokens.EntityHandle(entryToken)));

var graph = new CallGraph(set);
graph.Run(entry);
Console.WriteLine($"Reached {graph.Reached.Count} methods, {graph.Instantiated.Count} instantiated types, {graph.VirtualSlots.Count} virtual slots.");

// --- Static feature tags -------------------------------------------------
var tags = new Dictionary<MethodId, SortedSet<string>>();
var pinvokes = new Dictionary<MethodId, (string Dll, string Entry)>();
foreach (MethodId method in graph.Reached.Keys)
{
    graph.Bodies.TryGetValue(method, out List<Instruction>? body);
    tags[method] = Features.Classify(set, method, body, out (string, string)? import);
    if (import is not null) pinvokes[method] = import.Value;
}

// Declared P/Invoke surface of the Windows Desktop assemblies that were loaded.
var declared = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
var declaredByAssembly = new SortedDictionary<string, int>(StringComparer.Ordinal);
foreach (LoadedAssembly assembly in set.All.ToArray())
{
    if (!assembly.Path.StartsWith(desktop, StringComparison.OrdinalIgnoreCase)) continue;
    MetadataReader md = assembly.Metadata;
    foreach (MethodDefinitionHandle handle in md.MethodDefinitions)
    {
        MethodDefinition definition = md.GetMethodDefinition(handle);
        if ((definition.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
        MethodImport import = definition.GetImport();
        string dll = Features.NormalizeDll(md.GetString(md.GetModuleReference(import.Module).Name));
        string name = import.Name.IsNil ? md.GetString(definition.Name) : md.GetString(import.Name);
        if (!declared.TryGetValue(dll, out SortedSet<string>? names)) declared[dll] = names = new(StringComparer.Ordinal);
        names.Add(name);
        declaredByAssembly[assembly.Name] = declaredByAssembly.GetValueOrDefault(assembly.Name) + 1;
    }
}

// --- Compiler verdicts ---------------------------------------------------
// Include the browser launch framework selection and every assembly that owns
// reached code. The Thread facade is required for binding even though its
// forwarded type's methods belong to CoreLib. A rejected module initializer
// would fail every compile at [module, 0, -1]; it is removed and recorded.
string[] launchFramework =
[
    "System.Windows.Forms", "System.Windows.Forms.Primitives", "System.Private.Windows.Core", "System.Drawing.Common",
    "System.Private.Windows.GdiPlus", "System.Private.CoreLib", "System.Runtime", "System.ComponentModel.Primitives",
    "System.Collections", "System.Threading", "System.Drawing.Primitives", "System.ComponentModel",
    "System.ComponentModel.TypeConverter", "System.Collections.Concurrent", "System.Collections.Specialized", "System.ObjectModel",
    "System.Threading.Thread",
];
var pack = new List<LoadedAssembly> { app };
foreach (string name in launchFramework)
    if (set.ByName(name) is { } loaded && !pack.Contains(loaded)) pack.Add(loaded);
foreach (int owner in graph.Reached.Keys.Select(m => m.Assembly).Distinct().OrderBy(i => set.All[i].Name, StringComparer.Ordinal))
    if (!pack.Contains(set.All[owner])) pack.Add(set.All[owner]);
var excluded = new List<object>();
CompilerVerdicts verdicts = new(set, pack);
Verdict entryVerdict = verdicts.Compile(entry);
while (compile && entryVerdict.Status <= 0 && entryVerdict.Site is { Row: 0 } moduleSite && set.All[moduleSite.Assembly] != app)
{
    LoadedAssembly rejected = set.All[moduleSite.Assembly];
    excluded.Add(new { assembly = rejected.Name, status = entryVerdict.Status, reason = "module-level rejection while preparing the bundle" });
    pack.Remove(rejected);
    verdicts = new(set, pack);
    entryVerdict = verdicts.Compile(entry);
}
Console.WriteLine($"Entry verdict: {entryVerdict.Status} at {Location(entryVerdict)} ({entryVerdict.Milliseconds:F0} ms)");

var results = new ConcurrentDictionary<MethodId, Verdict>();
var packIndices = pack.Select(a => a.Index).ToHashSet();
List<MethodId> judged = graph.Reached.Keys
    .Where(m => set.Definition(m).RelativeVirtualAddress != 0 && packIndices.Contains(m.Assembly))
    .OrderBy(m => m.Assembly).ThenBy(m => m.Row).Take(limit).ToList();
// A verdict depends on the exact compiler and the exact bundle bytes. MethodDef
// rows and IL offsets only identify code within those bytes, so reuse requires
// the same compiler assembly hash and the same ordered bundle hashes. The
// bundle order is the pack order, and every main image is drawn from the pack.
string Sha256(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
string compilerPath = typeof(BootstrapLibrary.CompilerExports).Assembly.Location;
var verdictIdentity = new VerdictIdentity(
    Path.GetFileName(compilerPath), Sha256(File.ReadAllBytes(compilerPath)),
    pack.Select(a => new BundleIdentity(a.Name, Sha256(a.Bytes))).ToList());
if (reuse is not null)
{
    // Replays the verdicts of an earlier census.json for the same inputs, so the
    // rankings can be regenerated without recompiling every reached method.
    var byName = set.All.ToDictionary(a => a.Name, a => a.Index, StringComparer.Ordinal);
    var judgedSet = judged.ToHashSet();
    using JsonDocument previous = JsonDocument.Parse(File.ReadAllBytes(reuse));
    string? mismatch = VerdictIdentity.Compare(previous.RootElement, verdictIdentity);
    if (mismatch is not null)
    {
        Console.Error.WriteLine($"Refusing to reuse {reuse}: {mismatch}. Run without reuse to compile current verdicts.");
        return 3;
    }
    foreach (JsonElement row in previous.RootElement.GetProperty("methods").EnumerateArray())
    {
        if (row.GetProperty("compiler").ValueKind != JsonValueKind.Object) continue;
        string[] id = row.GetProperty("id").GetString()!.Split('#');
        var method = new MethodId(byName[id[0]], int.Parse(id[1]));
        if (!judgedSet.Contains(method)) continue;
        JsonElement c = row.GetProperty("compiler");
        int status = c.GetProperty("status").GetInt32();
        MethodId? site = null;
        if (c.GetProperty("site").GetString() is { } text &&
            System.Text.RegularExpressions.Regex.Match(text, @"^\[(.+), MethodDef (\d+), IL ") is { Success: true } match)
            site = new MethodId(byName[match.Groups[1].Value], int.Parse(match.Groups[2].Value));
        results[method] = new Verdict(status, site, c.GetProperty("offset").GetInt32(), 0);
    }
    Console.WriteLine($"Reused {results.Count} verdicts from {reuse}.");
}
else if (compile)
{
    int done = 0;
    var clock = System.Diagnostics.Stopwatch.StartNew();
    Parallel.ForEach(judged, new ParallelOptions { MaxDegreeOfParallelism = parallel }, method =>
    {
        results[method] = verdicts.Compile(method);
        int count = Interlocked.Increment(ref done);
        if (count % 500 == 0) Console.WriteLine($"  {count}/{judged.Count} verdicts ({clock.Elapsed.TotalSeconds:F0} s)");
    });
}

// --- Site classification -------------------------------------------------
string Location(Verdict v) => v.Site is null ? "[module]" : $"[{set.All[v.Site.Value.Assembly].Name}, MethodDef {v.Site.Value.Row}, IL {(v.Offset < 0 ? "-1" : $"0x{v.Offset:x4}")}]";

MethodLevelEvidence evidence = null!; // Built once every verdict is known.
var siteCache = new Dictionary<(MethodId, int, bool), (string Category, string Detail)>();
(string Category, string Detail) ClassifySite(MethodId site, int offset, MethodId export)
{
    // The export-ABI category applies only when the site is the export itself.
    var key = (site, offset, site == export);
    if (siteCache.TryGetValue(key, out var cached)) return cached;
    var result = ClassifySiteCore(site, offset, export);
    siteCache[key] = result;
    return result;
}

(string Category, string Detail) ClassifySiteCore(MethodId site, int offset, MethodId export)
{
    if (site.Row == 0) return ("module initializer (assembly-level)", set.All[site.Assembly].Name);
    MethodDefinition definition = set.Definition(site);
    if (offset < 0)
    {
        if (site == export && verdicts.ExportShapeRejected(site)) return ("export ABI (no body verdict)", "");
        if ((definition.Attributes & MethodAttributes.PinvokeImpl) != 0) return ("P/Invoke target", "");
        if ((definition.ImplAttributes & MethodImplAttributes.InternalCall) != 0) return ("runtime internal call", "");
        if ((definition.ImplAttributes & MethodImplAttributes.CodeTypeMask) == MethodImplAttributes.Runtime) return ("runtime-implemented (delegate) method", "");
        if (definition.RelativeVirtualAddress == 0) return ("no IL body (abstract or extern)", "");
        if (definition.Name is var n && set.MethodName(site) == ".cctor") return ("static constructor body/header", "");
        if (evidence.Explain(site) is { } suspect) return ("method-level: " + suspect.Kind, suspect.Type);
        return ("method-level: no distinguishing signature or local type", "");
    }
    LoadedAssembly assembly = set.All[site.Assembly];
    MethodBodyBlock body = assembly.Pe.GetMethodBody(definition.RelativeVirtualAddress);
    foreach (ExceptionRegion region in body.ExceptionRegions)
        if (region.HandlerOffset == offset || region.Kind == ExceptionRegionKind.Filter && region.FilterOffset == offset)
            return region.Kind switch
            {
                ExceptionRegionKind.Filter => ("exception filter", ""),
                ExceptionRegionKind.Fault => ("try/fault", ""),
                ExceptionRegionKind.Catch => ("catch clause type", Describe(assembly, region.CatchType)),
                _ => ("try/finally", ""),
            };
    List<Instruction> instructions = graph.Bodies.GetValueOrDefault(site) ?? IlInstructions.Decode(body);
    int index = instructions.FindIndex(i => i.Offset == offset);
    if (index < 0) return ("IL offset outside an instruction boundary", $"0x{offset:x4}");
    Instruction instruction = instructions[index];
    string op = instruction.OpCode.Name!;
    if (instruction.Token == 0) return ("opcode: " + op, "");
    EntityHandle token = IlInstructions.Entity(instruction.Token);
    if (op is "call" or "callvirt" or "newobj" or "ldftn" or "ldvirtftn" or "jmp")
    {
        MethodId? target = set.ResolveMethod(assembly, token, out bool viaSpec);
        if (target is null) return ("unresolved member reference", $"0x{instruction.Token:x8}");
        TypeFacts type = set.TypeOf(target.Value);
        MethodDefinition targetDefinition = set.Definition(target.Value);
        string detail = set.Display(target.Value);
        bool isStatic = (targetDefinition.Attributes & MethodAttributes.Static) != 0;
        if ((targetDefinition.Attributes & MethodAttributes.PinvokeImpl) != 0) return ("call to P/Invoke", detail);
        if ((targetDefinition.ImplAttributes & MethodImplAttributes.InternalCall) != 0) return ("call to runtime internal call", detail);
        if (op is "ldftn" or "ldvirtftn") return ("delegate target (" + op + ")", detail);
        if (op == "newobj" && type.IsDelegate) return ("delegate construction", detail);
        if (op == "newobj" && type.IsValueType) return ("value-type newobj", detail);
        if (viaSpec) return ("generic method instantiation", detail);
        if (op == "call" && type.IsValueType && !isStatic) return ("value-type instance call (managed receiver address)", detail);
        if (op == "callvirt" && type.IsInterface) return ("interface dispatch", detail);
        if (op == "callvirt" && (targetDefinition.Attributes & MethodAttributes.Virtual) != 0) return ("virtual dispatch (receiver not proven exact)", detail);
        if (type.IsGenericDefinition) return ("member of generic type", detail);
        if (isStatic && type.StaticConstructor is not null && !type.IsBeforeFieldInit) return ("static call that triggers a type initializer", detail);
        if (type.IsValueType) return ("value-type static call", detail);
        if (op == "newobj") return ("reference newobj (constructor or signature)", detail);
        return (op + " (linked target or signature)", detail);
    }
    if (op is "ldfld" or "ldflda" or "stfld" or "ldsfld" or "ldsflda" or "stsfld")
    {
        (TypeId? owner, string field) = set.ResolveFieldOwner(assembly, token);
        string detail = owner is null ? field : set.Type(owner.Value).FullName + "::" + field;
        bool isStaticField = op.StartsWith("lds") || op.StartsWith("sts");
        if (owner is not null && set.Type(owner.Value).IsExplicitLayout) return ("explicit-layout field", detail);
        if (isStaticField && owner is not null && set.Type(owner.Value).StaticConstructor is not null) return ("static field with type initializer", detail);
        if (isStaticField) return ("static field", detail);
        if (owner is not null && set.Type(owner.Value).IsValueType) return ("value-type field " + op, detail);
        return ("instance field " + op, detail);
    }
    if (op is "ldtoken") return ("ldtoken (runtime handles)", Describe(assembly, token));
    return ("opcode: " + op, Describe(assembly, token));
}

string Describe(LoadedAssembly assembly, EntityHandle handle)
{
    try
    {
        if (handle.Kind is HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification)
        {
            TypeId? type = set.ResolveType(assembly, handle);
            return type is null ? handle.Kind.ToString() : set.Type(type.Value).FullName;
        }
        if (handle.Kind == HandleKind.UserString) return "string";
    }
    catch (BadImageFormatException) { }
    return handle.Kind.ToString();
}

evidence = new MethodLevelEvidence(set, results, tags);

// --- Aggregate ------------------------------------------------------------
string Id(MethodId m) => $"{set.All[m.Assembly].Name}#{m.Row}";
var methods = new List<object>();
var siteGroups = new Dictionary<(MethodId Site, int Offset), (List<MethodId> Methods, bool Own)>();
var categoryOwn = new Dictionary<string, int>();
var categoryClosure = new Dictionary<string, int>();
int compiled = 0, ownBlocked = 0, closureBlocked = 0, exportShape = 0, notJudged = 0;
foreach ((MethodId method, Reason reason) in graph.Reached.OrderBy(p => p.Key.Assembly).ThenBy(p => p.Key.Row))
{
    results.TryGetValue(method, out Verdict? verdict);
    string? status = null, category = null, detail = null, site = null;
    if (verdict is null) { status = "not judged"; notJudged++; }
    else if (verdict.Status > 0) { status = "compiles"; compiled++; }
    else if (verdict.Site is { } s)
    {
        (category, detail) = ClassifySite(s, verdict.Offset, method);
        site = Location(verdict);
        if (category == "export ABI (no body verdict)") { status = "export ABI (no body verdict)"; exportShape++; }
        else if (s == method) { status = "blocked in own body"; ownBlocked++; categoryOwn[category] = categoryOwn.GetValueOrDefault(category) + 1; }
        else { status = "blocked in closure"; closureBlocked++; }
        if (status != "export ABI (no body verdict)")
        {
            categoryClosure[category] = categoryClosure.GetValueOrDefault(category) + 1;
            if (!siteGroups.TryGetValue((s, verdict.Offset), out var group)) siteGroups[(s, verdict.Offset)] = group = (new(), false);
            group.Methods.Add(method);
        }
    }
    else { status = "failed without location"; }
    methods.Add(new
    {
        id = Id(method),
        assembly = set.All[method.Assembly].Name,
        methodDef = method.Row,
        token = $"0x{0x06000000 | method.Row:x8}",
        name = set.Display(method),
        reachedBy = reason.Kind,
        reachedFrom = reason.From is null ? null : Id(reason.From.Value),
        reachedAt = reason.Offset,
        tags = tags[method],
        pinvoke = pinvokes.TryGetValue(method, out var p) ? new { dll = p.Dll, entry = p.Entry } : null,
        compiler = verdict is null ? null : new { status = verdict.Status, site, offset = verdict.Offset, category, detail },
        verdict = status,
    });
}

// Per-site and per-category counts of reached methods each blocks.
var ownBySite = results.Where(r => r.Value.Status <= 0 && r.Value.Site == r.Key)
    .GroupBy(r => (r.Value.Site!.Value, r.Value.Offset)).ToDictionary(g => g.Key, g => g.Count());
var sites = siteGroups.Select(g =>
{
    (string category, string detail) = ClassifySite(g.Key.Site, g.Key.Offset, default);
    return new
    {
        site = g.Key.Site.Row == 0 ? set.All[g.Key.Site.Assembly].Name : Id(g.Key.Site),
        method = g.Key.Site.Row == 0 ? "<module>" : set.Display(g.Key.Site),
        offset = g.Key.Offset,
        category,
        detail,
        blocksReachedMethods = g.Value.Methods.Count,
        reachedSite = graph.Reached.ContainsKey(g.Key.Site),
    };
}).OrderByDescending(s => s.blocksReachedMethods).ThenBy(s => s.site, StringComparer.Ordinal).ThenBy(s => s.offset).ToList();

var tagCounts = tags.SelectMany(t => t.Value.Select(tag => (tag, t.Key)))
    .GroupBy(x => x.tag)
    .Select(g => new
    {
        feature = g.Key,
        reachedMethods = g.Count(),
        blockedInOwnBody = g.Count(x => results.TryGetValue(x.Key, out Verdict? v) && v.Status <= 0 && v.Site == x.Key),
    })
    .OrderByDescending(x => x.reachedMethods).ThenBy(x => x.feature, StringComparer.Ordinal).ToList();

var reachedPinvokes = pinvokes.Values.GroupBy(p => p.Dll).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
    .Select(g => new
    {
        dll = g.Key,
        reachedEntryPoints = g.Select(p => p.Entry).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
        declaredByWindowsDesktop = declared.TryGetValue(g.Key, out SortedSet<string>? names) ? names.Count : 0,
        reachedFromWindowsDesktop = pinvokes.Where(p => p.Value.Dll == g.Key && set.All[p.Key.Assembly].Path.StartsWith(desktop, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Value.Entry).Distinct().Count(),
    }).ToList();

// Reached is filled in breadth-first order, so the first reached method of an
// assembly has a short witness path; it explains why the assembly is in scope.
List<string> WitnessPath(MethodId method)
{
    var path = new List<string>();
    for (MethodId? current = method; current is not null && path.Count < 40; current = graph.Reached[current.Value].From)
        path.Add($"{set.Display(current.Value)} ({graph.Reached[current.Value].Kind})");
    path.Reverse();
    return path;
}
var byAssembly = graph.Reached.Keys.GroupBy(m => set.All[m.Assembly].Name).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
    .Select(g => new { assembly = g.Key, reachedMethods = g.Count(), firstReachedPath = WitnessPath(g.First()) }).ToList();

var report = new
{
    schema = "standardwasm.winforms-census/2",
    entry = new { id = Id(entry), name = set.Display(entry) },
    inputs = set.All.OrderBy(a => a.Name, StringComparer.Ordinal).Select(a => new
    {
        name = a.Name,
        version = a.Metadata.GetAssemblyDefinition().Version.ToString(),
        file = Path.GetFileName(a.Path),
        source = a.Path.StartsWith(desktop, StringComparison.OrdinalIgnoreCase) ? "Microsoft.WindowsDesktop.App"
            : a.Path.StartsWith(shared, StringComparison.OrdinalIgnoreCase) ? "Microsoft.NETCore.App" : "application",
        sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(a.Bytes)).ToLowerInvariant(),
        inCompilerPack = pack.Contains(a),
    }).ToList(),
    unresolvedAssemblies = set.UnresolvedAssemblies,
    excludedFromCompilerPack = excluded,
    verdictIdentity,
    graph = new
    {
        model = "RTA: call/newobj/ldftn, callvirt/ldvirtftn to overrides in instantiated types, type and module initializers, finalizers",
        reachedMethods = graph.Reached.Count,
        withIlBody = graph.Bodies.Count,
        instantiatedTypes = graph.Instantiated.Count,
        finalizableTypes = graph.Finalizers.Select(pair => new
        {
            assembly = set.All[pair.Key.Assembly].Name,
            type = set.Type(pair.Key).FullName,
            id = Id(pair.Value),
            implementation = set.Display(pair.Value),
            inherited = set.TypeOf(pair.Value).Id != pair.Key,
        }).OrderBy(item => item.assembly, StringComparer.Ordinal).ThenBy(item => item.type, StringComparer.Ordinal).ToList(),
        initializedTypes = graph.InitializedTypes.Count,
        virtualSlots = graph.VirtualSlots.Count,
        unresolvedReferences = graph.UnresolvedCount,
        unresolvedSamples = graph.UnresolvedTokens,
        byAssembly,
    },
    compiler = new
    {
        entry = new { status = entryVerdict.Status, site = Location(entryVerdict), category = entryVerdict.Site is { } es ? ClassifySite(es, entryVerdict.Offset, entry).Category : null,
            detail = entryVerdict.Site is { } ed ? ClassifySite(ed, entryVerdict.Offset, entry).Detail : null },
        judged = results.Count,
        compiles = compiled,
        blockedInOwnBody = ownBlocked,
        blockedInClosure = closureBlocked,
        exportAbiNoVerdict = exportShape,
        notJudged,
        methodLevelSuspects = evidence.TopSuspects(60),
        categoriesByOwnBody = categoryOwn.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => new { category = x.Key, methods = x.Value }),
        categoriesByFirstBlocker = categoryClosure.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => new { category = x.Key, methods = x.Value }),
        sites,
    },
    features = tagCounts,
    pinvoke = new
    {
        reachedMethods = pinvokes.Count,
        reachedDistinctEntryPoints = pinvokes.Values.Distinct().Count(),
        declaredDistinctEntryPoints = declared.Sum(d => d.Value.Count),
        declaredPinvokeMethodsByAssembly = declaredByAssembly,
        byDll = reachedPinvokes,
        declaredOnlyByDll = declared.Select(d => new { dll = d.Key, declared = d.Value.Count,
            reached = pinvokes.Values.Where(p => p.Dll == d.Key).Select(p => p.Entry).Distinct().Count() }),
    },
    methods,
};
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = null };
string jsonPath = Path.Combine(output, "census.json");
File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, options));
// The compact summary omits the per-method list and keeps the 300 sites that
// block the most reached methods, so it is small enough to record with the stage.
var compactCompiler = new { report.compiler.entry, report.compiler.judged, report.compiler.compiles, report.compiler.blockedInOwnBody,
    report.compiler.blockedInClosure, report.compiler.exportAbiNoVerdict, report.compiler.notJudged, report.compiler.methodLevelSuspects,
    report.compiler.categoriesByOwnBody, report.compiler.categoriesByFirstBlocker, siteCount = sites.Count, sites = sites.Take(300) };
var compact = new { report.schema, report.entry, report.inputs, report.unresolvedAssemblies, report.excludedFromCompilerPack, report.verdictIdentity,
    report.graph, compiler = compactCompiler, report.features, report.pinvoke };
File.WriteAllText(Path.Combine(output, "census-summary.json"), JsonSerializer.Serialize(compact, options));
if (results.Count > 0)
    Console.WriteLine($"Compiler time: total {results.Values.Sum(v => v.Milliseconds) / 1000:F0} s, slowest {results.Values.Max(v => v.Milliseconds) / 1000:F1} s");
File.WriteAllText(Path.Combine(output, "summary.md"), Summary.Render(report, options));
Console.WriteLine("Wrote " + jsonPath);
return 0;
