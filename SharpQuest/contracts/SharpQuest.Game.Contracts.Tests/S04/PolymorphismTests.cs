namespace SharpQuest.Game.Tests.S04;

/// <summary>Session 4: the override actually decides, and one loop covers every kind.</summary>
public class PolymorphismTests : S04TestBase
{
    [Fact]
    public void Describe_says_something_for_every_kind()
    {
        foreach (var item in OnePerKind)
            Assert.False(string.IsNullOrWhiteSpace(Wiring.Describe(item)), $"Describe returned nothing for a {item.GetType().Name}.");
    }

    [Fact]
    public void Different_kinds_answer_differently()
    {
        NeedTwoKinds();
        var answers = OnePerKind.Select(i => (Kind: i.GetType().Name, Text: Wiring.Describe(i))).ToList();
        var shared = answers.GroupBy(a => a.Text, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        Assert.True(shared is null,
            $"{string.Join(" and ", shared?.Select(a => a.Kind) ?? [])} describe themselves exactly the same way (\"{shared?.Key}\"). " +
            "Each kind's override has to bring something of its own.");
    }

    [Fact]
    public void Describing_the_same_item_twice_gives_the_same_answer()
    {
        var item = Samples[0];

        Assert.Equal(Wiring.Describe(item), Wiring.Describe(item));
    }

    [Fact]
    public void DescribeAll_covers_every_item_in_order()
    {
        var items = Samples;

        var described = Wiring.DescribeAll(items);
        var expected = items.Select(Wiring.Describe).ToList();

        Assert.True(described.Count == items.Count, $"{items.Count} items went in, {described.Count} descriptions came out.");
        for (var i = 0; i < expected.Count; i++)
            Assert.True(described[i] == expected[i],
                $"Line {i} is \"{described[i]}\", but item {i} describes itself as \"{expected[i]}\". Keep the order, and let each item speak for itself.");
    }

    [Fact]
    public void DescribeAll_of_nothing_is_nothing()
    {
        Assert.Empty(Wiring.DescribeAll([]));
    }

    [Fact]
    public void DescribeAll_works_on_items_of_one_kind()
    {
        var kind = Kinds[0];
        var same = Samples.Where(s => s.GetType() == kind).Take(3).ToList();

        var described = Wiring.DescribeAll(same);

        Assert.Equal(same.Count, described.Count);
    }

    [Fact]
    public void DescribeAll_reads_its_items_once()
    {
        var reads = 0;
        IEnumerable<object> Counted() { foreach (var s in Samples) { reads++; yield return s; } }

        Wiring.DescribeAll(Counted());

        Assert.True(reads == Samples.Count,
            $"DescribeAll went through its items {(double)reads / Samples.Count:0.#} times. One loop over the base type is all it takes.");
    }
}
