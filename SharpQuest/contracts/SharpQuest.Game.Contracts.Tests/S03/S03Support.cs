using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;
using SharpQuest.Game.Contracts;

namespace SharpQuest.Game.Tests.S03;

/// <summary>The mutation results sq wrote to SharpQuest/.sq/mutation/S03.json before the tests ran.</summary>
public sealed record MutationResults(string? TestAssembly, bool BuildFailed, int OwnTotal, int OwnFailed, int SpecTotal, int SpecFailed, Dictionary<int, bool> Caught)
{
    /// <summary>The SharpQuest/ folder: the one with SharpQuest.Game.slnx in it.</summary>
    public static string SqRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "SharpQuest.Game.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Can't find SharpQuest.Game.slnx above " + AppContext.BaseDirectory);
    }

    private static string ResultsFile => Path.Combine(SqRoot(), ".sq", "mutation", "S03.json");

    /// <summary>Loads the results, or skips the calling test when sq hasn't produced them (a run from the IDE, say).</summary>
    public static MutationResults LoadOrSkip()
    {
        if (!File.Exists(ResultsFile))
            Assert.Skip("Run `sq test` (not plain dotnet test): it runs your tests against the broken versions first.");
        var j = JsonNode.Parse(File.ReadAllText(ResultsFile))!;
        var caught = (j["mutants"]?.AsObject() ?? [])
            .ToDictionary(kv => int.Parse(kv.Key), kv => (bool?)kv.Value?["caught"] == true);
        return new MutationResults((string?)j["testAssembly"], (bool?)j["buildFailed"] == true,
            (int?)j["ownTotal"] ?? 0, (int?)j["ownFailed"] ?? 0, (int?)j["specTotal"] ?? 0, (int?)j["specFailed"] ?? 0, caught);
    }

    /// <summary>
    /// Your compiled tests (Game.Core.Tests.dll): the one sq just built, from the mutation results. Only when sq hasn't run
    /// (a run from the IDE) the newest one under build/Tests/bin, and never when there are no test files left to build it from.
    /// </summary>
    public static string? FindTestAssembly()
    {
        if (!HasTestFiles()) return null;
        if (File.Exists(ResultsFile))
            return (string?)JsonNode.Parse(File.ReadAllText(ResultsFile))?["testAssembly"] is { } p && File.Exists(p) ? p : null;
        var bin = Path.Combine(SqRoot(), "build", "Tests", "bin");
        return Directory.Exists(bin)
            ? Directory.EnumerateFiles(bin, "Game.Core.Tests.dll", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
    }

    /// <summary>Are there any test files at all? Distinguishes "not written yet" from "didn't build".</summary>
    public static bool HasTestFiles() =>
        Directory.Exists(GameFiles.TestsDirectory)
        && Directory.EnumerateFiles(GameFiles.TestsDirectory, "*.cs", SearchOption.AllDirectories).Any();
}

/// <summary>What one of your test methods does, read from the compiled IL (nothing is executed).</summary>
public sealed class TestMethodFacts
{
    public required string Class { get; init; }
    public required string Name { get; init; }
    public int DataRows { get; set; }
    public bool HasDataSource { get; set; }
    public bool UsesOwnCode { get; set; }
    public bool UsesSearchMatcher { get; set; }
    public bool ChecksException { get; set; }
    public bool IsParameterised => DataRows >= 2 || HasDataSource;
    public override string ToString() => $"{Class}.{Name}";
}

/// <summary>
/// Reads your test assembly with System.Reflection.Metadata: which methods are NUnit tests, what they call
/// (following lambdas, async bodies and helpers), and whether anything peeks at the grader.
/// The game track's version of the Lab 03 reader: [Test]/[TestCase]/[TestCaseSource] instead of [Fact]/[Theory].
/// </summary>
public sealed class TestAssemblyFacts
{
    public List<TestMethodFacts> Tests { get; } = [];
    public List<string> Peeking { get; } = [];

    private const string SearchMatcherType = "SharpQuest.SearchMatcher";

    private static readonly Dictionary<ushort, OpCode> OpCodesByValue =
        typeof(OpCodes).GetFields().Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => (ushort)o.Value);

    private static readonly HashSet<string> ReflectionOnType =
        ["GetField", "GetFields", "GetProperty", "GetProperties", "GetMethod", "GetMethods", "GetMember", "GetMembers", "InvokeMember", "GetTypeInfo", "GetRuntimeFields"];

    private static readonly HashSet<string> TestMarkers = ["TestAttribute", "TestCaseAttribute", "TestCaseSourceAttribute", "TheoryAttribute"];
    private static readonly HashSet<string> ParameterSources = ["ValuesAttribute", "RangeAttribute", "ValueSourceAttribute", "RandomAttribute"];

    public static TestAssemblyFacts Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var facts = new TestAssemblyFacts();
        var walker = new Walker(md, pe);

        foreach (var th in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(th);
            var className = md.GetString(type.Name);

            if (className.Contains("SearchMatcher", StringComparison.OrdinalIgnoreCase))
            {
                // Everything in a SearchMatcher test class counts for peeking, constructors and set-ups included.
                foreach (var mh in type.GetMethods())
                    foreach (var p in walker.Scan(mh).Peeks) facts.Peeking.Add($"{className}: {p}");
                foreach (var nested in type.GetNestedTypes())
                    foreach (var mh in md.GetTypeDefinition(nested).GetMethods())
                        foreach (var p in walker.Scan(mh).Peeks) facts.Peeking.Add($"{className}: {p}");
            }

            foreach (var mh in type.GetMethods())
            {
                var method = md.GetMethodDefinition(mh);
                var name = md.GetString(method.Name);
                TestMethodFacts? test = null;
                foreach (var ch in method.GetCustomAttributes())
                {
                    var attr = walker.AttributeName(md.GetCustomAttribute(ch));
                    if (!TestMarkers.Contains(attr)) continue;
                    test ??= new TestMethodFacts { Class = className, Name = name };
                    if (attr == "TestCaseAttribute") test.DataRows++;
                    if (attr is "TestCaseSourceAttribute" or "TheoryAttribute") test.HasDataSource = true;
                }
                if (test is null) continue;
                foreach (var ph in method.GetParameters())
                    foreach (var ch in md.GetParameter(ph).GetCustomAttributes())
                        if (ParameterSources.Contains(walker.AttributeName(md.GetCustomAttribute(ch)))) test.HasDataSource = true;

                // The test body, plus compiler-generated bodies that belong to it (lambdas, async state machines).
                var scan = walker.Scan(mh);
                foreach (var related in RelatedBodies(md, th, name))
                    scan = scan.Merge(walker.Scan(related));

                test.UsesOwnCode = scan.OwnCode;
                test.UsesSearchMatcher = scan.SearchMatcher;
                test.ChecksException = scan.ExceptionCheck;
                facts.Tests.Add(test);
            }
        }
        return facts;
    }

    private static IEnumerable<MethodDefinitionHandle> RelatedBodies(MetadataReader md, TypeDefinitionHandle th, string methodName)
    {
        var marker = $"<{methodName}>";
        var stack = new Stack<TypeDefinitionHandle>([th]);
        while (stack.Count > 0)
        {
            var t = md.GetTypeDefinition(stack.Pop());
            var typeName = md.GetString(t.Name);
            foreach (var mh in t.GetMethods())
            {
                var n = md.GetString(md.GetMethodDefinition(mh).Name);
                if (n.Contains(marker) || (typeName.Contains(marker) && n == "MoveNext")) yield return mh;
            }
            foreach (var nested in t.GetNestedTypes()) stack.Push(nested);
        }
    }

    public sealed record Scan(bool OwnCode, bool SearchMatcher, bool ExceptionCheck, List<string> Peeks)
    {
        public Scan Merge(Scan o) => new(OwnCode || o.OwnCode, SearchMatcher || o.SearchMatcher, ExceptionCheck || o.ExceptionCheck, [.. Peeks, .. o.Peeks]);
    }

    /// <summary>NUnit's ways of saying "this must throw": Assert.Throws/ThrowsAsync/Catch/CatchAsync, and the Throws.… constraints.</summary>
    private static bool IsExceptionCheck(string owner, string member) =>
        (owner == "Assert" && (member.StartsWith("Throws") || member.StartsWith("Catch"))) || owner == "Throws";

    private sealed class Walker(MetadataReader md, PEReader pe)
    {
        private readonly Dictionary<MethodDefinitionHandle, Scan> _cache = [];
        private readonly HashSet<MethodDefinitionHandle> _inProgress = [];

        public string AttributeName(CustomAttribute ca) => ca.Constructor.Kind switch
        {
            HandleKind.MethodDefinition => md.GetString(md.GetTypeDefinition(md.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor).GetDeclaringType()).Name),
            HandleKind.MemberReference => TypeOf(md.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent).Name,
            _ => "",
        };

        public Scan Scan(MethodDefinitionHandle mh)
        {
            if (_cache.TryGetValue(mh, out var cached)) return cached;
            if (!_inProgress.Add(mh)) return new(false, false, false, []);
            var result = new Scan(false, false, false, []);
            var method = md.GetMethodDefinition(mh);
            if (method.RelativeVirtualAddress != 0)
            {
                var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
                while (il.RemainingBytes > 0)
                {
                    int code = il.ReadByte();
                    if (code == 0xFE) code = 0xFE00 | il.ReadByte();
                    if (!OpCodesByValue.TryGetValue((ushort)code, out var op)) break;
                    switch (op.OperandType)
                    {
                        case OperandType.InlineNone: break;
                        case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar: il.ReadByte(); break;
                        case OperandType.InlineVar: il.ReadUInt16(); break;
                        case OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineSig: il.ReadInt32(); break;
                        case OperandType.ShortInlineR: il.ReadSingle(); break;
                        case OperandType.InlineR: il.ReadDouble(); break;
                        case OperandType.InlineI8: il.ReadInt64(); break;
                        case OperandType.InlineSwitch: var n = il.ReadInt32(); for (var i = 0; i < n; i++) il.ReadInt32(); break;
                        case OperandType.InlineString:
                            var s = md.GetUserString(MetadataTokens.UserStringHandle(il.ReadInt32() & 0xFFFFFF));
                            if (s.Contains("SQ_MUTANT", StringComparison.OrdinalIgnoreCase)) result.Peeks.Add("mentions SQ_MUTANT");
                            break;
                        default:
                            result = result.Merge(Entity(MetadataTokens.EntityHandle(il.ReadInt32())));
                            break;
                    }
                }
            }
            _inProgress.Remove(mh);
            return _cache[mh] = result;
        }

        private Scan Entity(EntityHandle h)
        {
            switch (h.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    // A method in the test assembly itself: a helper, so follow it.
                    var def = md.GetMethodDefinition((MethodDefinitionHandle)h);
                    var owner = TypeOf(def.GetDeclaringType());
                    var check = IsExceptionCheck(owner.Name, md.GetString(def.Name));
                    return Scan((MethodDefinitionHandle)h).Merge(new Scan(false, false, check, []));
                }
                case HandleKind.MethodSpecification:
                    var spec = md.GetMethodSpecification((MethodSpecificationHandle)h);
                    var args = spec.DecodeSignature(new Names(), null);
                    return Entity(spec.Method).Merge(FromTypeNames(args));
                case HandleKind.MemberReference:
                    var mr = md.GetMemberReference((MemberReferenceHandle)h);
                    var parent = TypeOf(mr.Parent);
                    var member = md.GetString(mr.Name);
                    var scan = FromType(parent);
                    var peeks = new List<string>();
                    if (parent.Full == "System.Environment" && member.StartsWith("GetEnvironmentVariable")) peeks.Add("reads environment variables");
                    if (parent.Full == "System.Type" && ReflectionOnType.Contains(member)) peeks.Add($"uses reflection (Type.{member})");
                    return scan.Merge(new Scan(false, false, IsExceptionCheck(parent.Name, member), peeks));
                case HandleKind.TypeReference or HandleKind.TypeDefinition or HandleKind.TypeSpecification:
                    return FromType(TypeOf(h));
                default:
                    return new(false, false, false, []);
            }
        }

        private static Scan FromTypeNames(IEnumerable<string> names) =>
            names.Select(n => new Scan(IsOwn(n), n.Contains(SearchMatcherType), false, []))
                 .Aggregate(new Scan(false, false, false, []), (a, b) => a.Merge(b));

        private static Scan FromType(TypeName t)
        {
            var peeks = t.Full.StartsWith("System.Reflection") && !t.Full.StartsWith("System.Reflection.Metadata")
                ? new List<string> { $"uses reflection ({t.Full})" } : [];
            return new Scan(IsOwn(t.Assembly + "!" + t.Full) || t.GenericArgs.Any(IsOwn),
                            t.Full == SearchMatcherType || t.GenericArgs.Any(a => a.Contains(SearchMatcherType)),
                            false, peeks);
        }

        /// <summary>Your own code: anything from Game.Core except what sq put there (namespace SharpQuest).</summary>
        private static bool IsOwn(string qualified) =>
            qualified.Contains("Game.Core!") && !qualified.Contains("Game.Core!SharpQuest.") && !qualified.Contains("Game.Core!System.");

        public TypeName TypeOf(EntityHandle h)
        {
            switch (h.Kind)
            {
                case HandleKind.TypeReference:
                {
                    var tr = md.GetTypeReference((TypeReferenceHandle)h);
                    var name = md.GetString(tr.Name);
                    var ns = md.GetString(tr.Namespace);
                    var scope = tr.ResolutionScope;
                    while (scope.Kind == HandleKind.TypeReference)   // nested type: climb to the outer one
                    {
                        var outer = md.GetTypeReference((TypeReferenceHandle)scope);
                        ns = md.GetString(outer.Namespace);
                        scope = outer.ResolutionScope;
                    }
                    var asm = scope.Kind == HandleKind.AssemblyReference
                        ? md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) : "";
                    return new TypeName(asm, ns, name, []);
                }
                case HandleKind.TypeDefinition:
                {
                    var td = md.GetTypeDefinition((TypeDefinitionHandle)h);
                    return new TypeName("", md.GetString(td.Namespace), md.GetString(td.Name), []);
                }
                case HandleKind.TypeSpecification:
                {
                    var decoded = md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(new Names(), null);
                    // "asm!ns.Name`1<asm!arg,...>" → the generic definition plus its arguments
                    var lt = decoded.IndexOf('<');
                    var head = lt < 0 ? decoded : decoded[..lt];
                    var args = lt < 0 ? [] : decoded[(lt + 1)..^1].Split(',').ToList();
                    var bang = head.IndexOf('!');
                    var full = bang < 0 ? head : head[(bang + 1)..];
                    var dot = full.LastIndexOf('.');
                    return new TypeName(bang < 0 ? "" : head[..bang], dot < 0 ? "" : full[..dot], dot < 0 ? full : full[(dot + 1)..], args);
                }
                case HandleKind.MethodDefinition:
                    return TypeOf(md.GetMethodDefinition((MethodDefinitionHandle)h).GetDeclaringType());
                default:
                    return new TypeName("", "", "", []);
            }
        }
    }

    public sealed record TypeName(string Assembly, string Namespace, string Name, List<string> GenericArgs)
    {
        public string Full => Namespace.Length == 0 ? Name : $"{Namespace}.{Name}";
    }

    /// <summary>Turns signature types into "assembly!namespace.Name" strings.</summary>
    private sealed class Names : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System!" + typeCode;
        public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var t = r.GetTypeDefinition(handle);
            return "!" + r.GetString(t.Namespace) + "." + r.GetString(t.Name);
        }
        public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var t = r.GetTypeReference(handle);
            var ns = r.GetString(t.Namespace);
            var scope = t.ResolutionScope;
            while (scope.Kind == HandleKind.TypeReference)
            {
                var outer = r.GetTypeReference((TypeReferenceHandle)scope);
                ns = r.GetString(outer.Namespace);
                scope = outer.ResolutionScope;
            }
            var asm = scope.Kind == HandleKind.AssemblyReference ? r.GetString(r.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) : "";
            return $"{asm}!{ns}.{r.GetString(t.Name)}";
        }
        public string GetTypeFromSpecification(MetadataReader r, object? ctx, TypeSpecificationHandle handle, byte rawTypeKind) =>
            r.GetTypeSpecification(handle).DecodeSignature(this, ctx);
        public string GetSZArrayType(string elementType) => elementType;
        public string GetArrayType(string elementType, ArrayShape shape) => elementType;
        public string GetByReferenceType(string elementType) => elementType;
        public string GetPointerType(string elementType) => elementType;
        public string GetPinnedType(string elementType) => elementType;
        public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments) =>
            genericType + "<" + string.Join(",", typeArguments) + ">";
        public string GetGenericTypeParameter(object? ctx, int index) => "!T" + index;
        public string GetGenericMethodParameter(object? ctx, int index) => "!M" + index;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    }
}
