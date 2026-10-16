using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S04;

/// <summary>Session 4: the shape of the family - an abstract base and two or more kinds that override it.</summary>
public class HierarchyTests : S04TestBase
{
    [Fact]
    public void Wiring_class_can_be_created()
    {
        Assert.NotNull(Wiring);
    }

    [Fact]
    public void The_base_is_an_abstract_class_in_game_core()
    {
        var b = BaseType;

        Assert.True(GameCode.IsGameCore(b), $"{b.FullName} isn't in Game.Core. The family belongs in Assets/Game/Core/.");
        Assert.True(b.IsClass && !b.IsInterface, $"{b.Name} is not a class. Session 4 is about inheritance: an abstract class.");
        Assert.True(b.IsAbstract, $"{b.Name} is not abstract. Mark it 'abstract', so nothing can be just \"an enemy\" without a kind.");
    }

    [Fact]
    public void The_base_declares_something_for_the_kinds_to_override()
    {
        var members = PolymorphicMembers(BaseType);

        Assert.True(members.Count > 0,
            $"{BaseType.Name} declares no abstract or virtual member of its own. Add the rule each kind decides for itself, " +
            "e.g. 'public abstract int DamageTaken(int amount, DamageType type);'.");
    }

    [Fact]
    public void The_samples_cover_at_least_two_kinds()
    {
        NeedTwoKinds();
    }

    [Fact]
    public void Every_kind_is_a_concrete_class_derived_from_the_base()
    {
        foreach (var kind in Kinds)
        {
            Assert.True(kind != BaseType, $"A sample is a plain {BaseType.Name}. Every item must be one of your kinds.");
            Assert.True(BaseType.IsAssignableFrom(kind), $"{kind.Name} does not derive from {BaseType.Name}.");
            Assert.True(GameCode.IsGameCore(kind), $"{kind.Name} isn't in Game.Core.");
        }
    }

    [Fact]
    public void Every_kind_overrides_a_member_of_the_base()
    {
        var members = PolymorphicMembers(BaseType);
        if (members.Count == 0) Assert.Fail($"{BaseType.Name} has nothing to override yet (see the test above).");

        foreach (var kind in Kinds)
            Assert.True(OverridesIn(kind, BaseType, members).Count > 0,
                $"{kind.Name} overrides none of {BaseType.Name}'s members ({string.Join(", ", members.Select(Show))}). " +
                "Without an override, two kinds behave identically and the hierarchy buys you nothing.");
    }

    [Fact]
    public void At_least_one_overridden_member_is_a_rule_not_a_label()
    {
        var members = PolymorphicMembers(BaseType);
        var overridden = members.Where(m => Kinds.Any(k => OverridesIn(k, BaseType, [m]).Count > 0)).ToList();
        if (overridden.Count == 0) Assert.Fail($"None of {BaseType.Name}'s members is overridden yet (see the test above).");

        var rules = overridden.Where(m => m.GetParameters().Length > 0 || m.ReturnType != typeof(string)).ToList();

        Assert.True(rules.Count > 0,
            $"Every member your kinds override returns a string and takes nothing ({string.Join(", ", overridden.Select(Show))}): " +
            "they describe themselves, but no kind decides anything. In a game the override is a rule: how much damage this kind " +
            "takes, whether it can pass this gate, where it moves next. Add one, e.g. 'public abstract int DamageTaken(int amount, DamageType type);'.");
    }
}
