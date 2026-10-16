using System.Text.RegularExpressions;

namespace SharpQuest.Game.Tests.S05;

/// <summary>Session 5: Matching, an iterator, and a lazy one.</summary>
public class IteratorTests : S05TestBase
{
    private static string FirstWordOf(string name) =>
        Regex.Matches(name, @"\p{L}{2,}").Select(m => m.Value).FirstOrDefault() ?? name;

    [Fact]
    public void Everything_matches_the_empty_text()
    {
        var collection = CollectionWith(0, 1, 2);

        Assert.Equal(IdsAt(0, 1, 2), Ids(Wiring.Matching(collection, "")));
    }

    [Fact]
    public void A_word_from_a_name_finds_that_item()
    {
        var collection = CollectionWith(3, 1);
        var wanted = Items[3];

        Assert.Contains(wanted, Wiring.Matching(collection, FirstWordOf(Wiring.NameOf(wanted))).ToList());
    }

    [Fact]
    public void Matching_ignores_case()
    {
        var collection = CollectionWith(3, 1);
        var wanted = Items[3];
        var shouted = FirstWordOf(Wiring.NameOf(wanted)).ToUpperInvariant();

        Assert.Contains(wanted, Wiring.Matching(collection, shouted).ToList());
    }

    [Fact]
    public void Text_nobody_has_matches_nothing()
    {
        Assert.Empty(Wiring.Matching(CollectionWith(0, 1, 2), "qqzzxx-nothing-is-called-this"));
    }

    [Fact]
    public void Matching_keeps_collection_order()
    {
        var collection = CollectionWith(3, 0, 2, 1);

        Assert.Equal(IdsAt(3, 0, 2, 1), Ids(Wiring.Matching(collection, "")));
    }

    [Fact]
    public void Nothing_happens_until_you_enumerate()
    {
        var collection = CollectionWith(0, 1);

        var matches = Wiring.Matching(collection, "");   // asked for, not enumerated
        Wiring.Add(collection, Items[2]);                 // the collection changes in between
        var count = matches.Count();

        Assert.True(count == 3,
            $"Enumerating after adding an item saw {count} items, not 3. An iterator written with 'yield return' does its work " +
            "while you enumerate it, so it sees the item added in between. Building a List inside Matching and returning it is what gives 2.");
    }

    [Fact]
    public void Matching_can_be_enumerated_more_than_once()
    {
        var matches = Wiring.Matching(CollectionWith(0, 1), "");

        Assert.Equal(Ids(matches), Ids(matches));
    }

    [Fact]
    public void Matching_does_not_change_the_collection()
    {
        var collection = CollectionWith(0, 1, 2);

        foreach (var _ in Wiring.Matching(collection, "")) { }

        Assert.Equal(IdsAt(0, 1, 2), Ids(EachInOrder(collection)));
    }
}
