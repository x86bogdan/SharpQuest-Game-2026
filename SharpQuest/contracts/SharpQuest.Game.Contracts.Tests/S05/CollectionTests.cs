using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S05;

/// <summary>Session 5: a collection of your own. foreach over it, index into it, add to it.</summary>
public class CollectionTests : S05TestBase
{
    [Fact]
    public void The_collection_is_yours()
    {
        Assert.True(GameCode.IsGameCore(CollectionType),
            $"{CollectionType.Name} lives in {CollectionType.Assembly.GetName().Name}. Write your collection in Game.Core: " +
            "a List<T> is somebody else's collection.");
    }

    [Fact]
    public void You_can_foreach_over_the_collection_itself()
    {
        var wanted = typeof(IEnumerable<>).MakeGenericType(Wiring.ItemType);
        Assert.True(wanted.IsAssignableFrom(CollectionType),
            $"{CollectionType.Name} does not implement IEnumerable<{Wiring.ItemType.Name}>, so 'foreach (var x in collection)' " +
            "won't compile. Implement it: GetEnumerator(), plus the old non-generic one that just calls it.");
    }

    [Fact]
    public void Something_in_the_collection_is_written_with_yield()
    {
        Assert.True(HasIteratorMember(CollectionType),
            $"{CollectionType.Name} contains no method written with 'yield return'. Handing back your list's enumerator " +
            "is not writing an iterator: write GetEnumerator (or Matching) with yield.");
    }

    [Fact]
    public void There_is_an_indexer_by_position_and_one_by_id()
    {
        Assert.True(Indexer(CollectionType, typeof(int)) is not null, $"{CollectionType.Name} has no 'this[int]' indexer.");
        Assert.True(Indexer(CollectionType, typeof(string)) is not null, $"{CollectionType.Name} has no 'this[string]' indexer.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void The_position_indexer_follows_the_order_items_were_added(int index)
    {
        int[] order = [3, 0, 2];
        var collection = CollectionWith(order);

        var expected = Items[order[index]];
        var actual = Wiring.At(collection, index);

        Assert.True(ReferenceEquals(expected, actual) || Wiring.IdOf(expected) == Wiring.IdOf(actual),
            $"[{index}] gave \"{Wiring.NameOf(actual)}\", but the item added at position {index} was \"{Wiring.NameOf(expected)}\". " +
            "The position indexer counts from the first item added.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void A_position_outside_the_collection_is_rejected(int index)
    {
        var collection = CollectionWith(0, 1, 2);

        var ex = Record.Exception(() => Wiring.At(collection, index));

        Assert.True(ex is not null, $"[{index}] on a collection of 3 returned instead of throwing.");
        Assert.IsAssignableFrom<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void The_id_indexer_finds_the_item()
    {
        var collection = CollectionWith(0, 1);
        var wanted = Items[1];

        Assert.Same(wanted, Wiring.ById(collection, Wiring.IdOf(wanted)));
    }

    [Fact]
    public void The_id_indexer_gives_null_for_an_id_nobody_has()
    {
        Assert.Null(Wiring.ById(CollectionWith(0, 1), "no-such-id"));
    }

    [Fact]
    public void Foreach_over_the_collection_sees_every_item_in_order()
    {
        var collection = CollectionWith(2, 0, 3, 1);

        Assert.Equal(IdsAt(2, 0, 3, 1), Ids(EachInOrder(collection)));
    }

    [Fact]
    public void Foreach_over_an_empty_collection_sees_nothing()
    {
        Assert.Empty(EachInOrder(CollectionWith()));
    }

    [Fact]
    public void Enumerating_twice_gives_the_same_answer()
    {
        var collection = CollectionWith(1, 3);

        Assert.Equal(Ids(EachInOrder(collection)), Ids(EachInOrder(collection)));
    }

    [Fact]
    public void Adding_after_you_enumerated_is_visible_next_time()
    {
        var collection = CollectionWith(0);

        var before = EachInOrder(collection).Count;
        Wiring.Add(collection, Items[1]);

        Assert.Equal(1, before);
        Assert.Equal(2, EachInOrder(collection).Count);
    }

    [Fact]
    public void Two_collections_do_not_share_their_items()
    {
        var first = CollectionWith(0, 1);
        var second = CollectionWith(2);

        Assert.Equal(IdsAt(0, 1), Ids(EachInOrder(first)));
        Assert.Equal(IdsAt(2), Ids(EachInOrder(second)));
    }
}
