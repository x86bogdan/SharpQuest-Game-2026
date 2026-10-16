using System.Reflection;
using SharpQuest.Game.Contracts.Grading;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S04;

/// <summary>Shared by every S04 test class: the wiring, the family it points at, and small helpers.</summary>
public abstract class S04TestBase
{
    private IKindsWiring? _wiring;
    private IReadOnlyList<object>? _samples;

    protected IKindsWiring Wiring => _wiring ??= GameCode.Wiring<IKindsWiring>("KindsWiring<YourBase>");

    protected Type BaseType => Wiring.BaseType;

    protected IReadOnlyList<object> Samples
    {
        get
        {
            if (_samples is not null) return _samples;
            var wiring = Wiring;
            IReadOnlyList<object>? samples = null;
            var ex = Record.Exception(() => samples = wiring.Samples());
            if (ex is not null) Assert.Fail($"Samples() threw {ex.GetType().Name}: {ex.Message}");
            Assert.True(samples is not null && samples.Count > 0, "Samples() returned nothing. Return items of at least two kinds.");
            Assert.True(samples!.All(s => s is not null), "Samples() contains a null.");
            return _samples = samples;
        }
    }

    /// <summary>The kinds the samples belong to: their runtime types, in order of first appearance.</summary>
    protected IReadOnlyList<Type> Kinds => Samples.Select(s => s.GetType()).Distinct().ToList();

    /// <summary>One sample of each kind.</summary>
    protected IReadOnlyList<object> OnePerKind => Kinds.Select(k => Samples.First(s => s.GetType() == k)).ToList();

    protected void NeedTwoKinds()
    {
        Assert.True(Kinds.Count >= 2,
            $"Samples() are all {Kinds[0].Name}. Return items of at least two different kinds, or the tests can't compare them.");
    }

    /// <summary>
    /// What the base declares for its kinds to override: its own abstract or virtual members (methods, property
    /// getters), minus object's (ToString, Equals, GetHashCode) and what a record generates for itself.
    /// </summary>
    protected static IReadOnlyList<MethodInfo> PolymorphicMembers(Type baseType) =>
        baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly) && m.IsVirtual && !m.IsFinal)
            .Where(m => m.GetBaseDefinition().DeclaringType == baseType)   // declared here, not an override of something older
            .Where(m => m.Name is not ("ToString" or "Equals" or "GetHashCode" or "get_EqualityContract" or "PrintMembers" or "<Clone>$"))
            .ToList();

    /// <summary>Does <paramref name="kind"/> (or a class between it and the base) override one of these members?</summary>
    protected static IReadOnlyList<MethodInfo> OverridesIn(Type kind, Type baseType, IReadOnlyList<MethodInfo> members)
    {
        var found = new List<MethodInfo>();
        for (var t = kind; t is not null && t != baseType; t = t.BaseType)
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (members.Any(b => m.GetBaseDefinition() == b)) found.Add(m);
        return found;
    }

    protected static string Show(MethodInfo m) => m.IsSpecialName && m.Name.StartsWith("get_", StringComparison.Ordinal) ? m.Name[4..] : m.Name + "()";
}
