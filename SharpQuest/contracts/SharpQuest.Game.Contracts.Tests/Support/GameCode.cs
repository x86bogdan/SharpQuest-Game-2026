using System.Reflection;
using System.Runtime.CompilerServices;
using SharpQuest.Game.Contracts;
using SharpQuest.Game.Contracts.Grading;

namespace SharpQuest.Game.Tests.Support;

/// <summary>
/// Finds things in the student's game without knowing their names. Shared by every session's tests.
/// The game track's counterpart of the labs' StudentCode: the rules are in Game.Core (compiled from the
/// Unity project's Assets/Game/Core/), the wiring in SharpQuest.Game.Wiring (SharpQuest/Wiring/).
/// </summary>
public static class GameCode
{
    public const string CoreAssemblyName = "Game.Core";
    public const string WiringAssemblyName = "SharpQuest.Game.Wiring";

    /// <summary>sq looks for this prefix to show a session as "not started".</summary>
    public const string NotStartedPrefix = "Not started:";

    public static Assembly Core { get; } = Assembly.Load(new AssemblyName(CoreAssemblyName));

    public static Assembly WiringAssembly { get; } = Assembly.Load(new AssemblyName(WiringAssemblyName));

    /// <summary>
    /// Creates the student's wiring for a session: the one class in SharpQuest/Wiring/ that derives from that
    /// session's base (and so implements <typeparamref name="TWiring"/>). None means the session hasn't started:
    /// every test that asks is SKIPPED, not failed. Call it inside a test method, never in a constructor.
    /// </summary>
    public static TWiring Wiring<TWiring>(string baseName) where TWiring : class, IWiring
    {
        var candidates = Types(WiringAssembly)
            .Where(t => t.IsClass && !t.IsAbstract && typeof(TWiring).IsAssignableFrom(t))
            .ToList();

        if (candidates.Count == 0)
            Assert.Skip($"{NotStartedPrefix} no class in SharpQuest/Wiring/ derives from {baseName}. Add one to start this session.");
        if (candidates.Count > 1)
            Assert.Fail($"{candidates.Count} classes derive from {baseName} ({string.Join(", ", candidates.Select(c => c.Name))}). Keep exactly one.");

        var type = candidates[0];
        if (type.GetConstructor(Type.EmptyTypes) is null)
            Assert.Fail($"{type.Name} needs a public constructor with no parameters.");

        return (TWiring)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// The student's own model: every type in Game.Core they wrote, minus compiler-generated types and the
    /// managed polyfills (namespaces System.* and SharpQuest.*, e.g. IsExternalInit).
    /// </summary>
    public static IReadOnlyList<Type> ModelTypes() =>
        Types(Core)
            .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Where(t => !t.Name.StartsWith('<'))
            .Where(t => !(t.Namespace is { } ns && (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)
                                                     || ns == "SharpQuest" || ns.StartsWith("SharpQuest.", StringComparison.Ordinal))))
            .Where(t => t.Name is not ("EmbeddedAttribute" or "NullableAttribute" or "NullableContextAttribute" or "RefSafetyRulesAttribute"))
            .ToList();

    /// <summary>Is this type the student's own, compiled from their Unity project's Core folder?</summary>
    public static bool IsGameCore(Type t) => t.Assembly == Core;

    /// <summary>Records are classes or structs the compiler gave a synthesized &lt;Clone&gt;$ / PrintMembers.</summary>
    public static bool IsRecord(Type t) =>
        t.GetMethod("<Clone>$") is not null
        || (t.IsValueType && t.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic) is not null
            && t.GetMethods().Any(m => m.Name == "op_Equality"));

    /// <summary>A path inside the repository, short enough to read in a test message.</summary>
    public static string Show(string path)
    {
        var project = GameFiles.ProjectDirectory;
        var parent = Path.GetDirectoryName(project.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? project;
        return Path.GetRelativePath(parent, path).Replace('\\', '/');
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.OfType<Type>(); }
    }
}
