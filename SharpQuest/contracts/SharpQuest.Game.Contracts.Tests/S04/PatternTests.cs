namespace SharpQuest.Game.Tests.S04;

/// <summary>Session 4: Classify - a switch expression with an arm per kind, a fallback, and a null arm.</summary>
public class PatternTests : S04TestBase
{
    [Fact]
    public void Every_kind_gets_a_label()
    {
        foreach (var item in OnePerKind)
            Assert.False(string.IsNullOrWhiteSpace(Wiring.Classify(item)), $"Classify returned nothing for a {item.GetType().Name}.");
    }

    [Fact]
    public void Items_of_the_same_kind_get_the_same_label()
    {
        foreach (var group in Samples.GroupBy(s => s.GetType()))
        {
            var labels = group.Select(Wiring.Classify).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(labels.Count == 1,
                $"Your {group.Key.Name}s get {labels.Count} different labels ({string.Join(", ", labels)}). " +
                "The label depends on the kind: one type pattern, one arm.");
        }
    }

    [Fact]
    public void Different_kinds_get_different_labels()
    {
        NeedTwoKinds();
        var labels = OnePerKind.Select(i => (Kind: i.GetType().Name, Label: Wiring.Classify(i))).ToList();
        var shared = labels.GroupBy(l => l.Label, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        Assert.True(shared is null,
            $"{string.Join(" and ", shared?.Select(l => l.Kind) ?? [])} share the label \"{shared?.Key}\". Each type pattern needs its own arm.");
    }

    [Fact]
    public void Null_is_a_programming_error()
    {
        var wiring = Wiring;   // resolve first, so "not started" skips instead of failing

        var ex = Record.Exception(() => wiring.Classify(null));

        Assert.True(ex is not null, "Classify(null) returned instead of throwing. Add the arm 'null => throw new ArgumentNullException(nameof(item))'.");
        Assert.IsAssignableFrom<ArgumentNullException>(ex);
    }

    [Fact]
    public void Classify_and_Describe_answer_different_questions()
    {
        foreach (var item in OnePerKind)
            Assert.True(Wiring.Classify(item) != Wiring.Describe(item),
                $"For a {item.GetType().Name}, Classify and Describe say the same thing. Describe is the item speaking for itself " +
                "(the override); Classify is the outside world sorting it into a category (the switch expression).");
    }

    [Fact]
    public void Classifying_changes_nothing()
    {
        var before = Samples.Select(Wiring.Describe).ToList();

        foreach (var item in Samples) Wiring.Classify(item);

        Assert.Equal(before, Samples.Select(Wiring.Describe));
    }
}
