using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpQuest.Game.Contracts.Grading;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S05;

/// <summary>Shared by the S05 test classes: the wiring, the collection type, and small helpers.</summary>
public abstract class S05TestBase
{
    private ICollectionWiring? _wiring;
    private IReadOnlyList<object>? _items;

    protected ICollectionWiring Wiring => _wiring ??= GameCode.Wiring<ICollectionWiring>("CollectionWiring<YourCollection, YourItem>");

    protected Type CollectionType => Wiring.CollectionType;

    /// <summary>The items to put in collections: four or more, with different Ids.</summary>
    protected IReadOnlyList<object> Items
    {
        get
        {
            if (_items is not null) return _items;
            var items = Wiring.Items();
            Assert.True(items is { Count: >= 4 }, $"Items() gave {items?.Count ?? 0} items. The tests need at least four different ones.");
            Assert.True(items!.Select(Wiring.IdOf).Distinct().Count() == items!.Count, "Items() gave two items with the same Id.");
            return _items = items;
        }
    }

    /// <summary>A new collection holding Items()[i] for each index, added in that order.</summary>
    protected object CollectionWith(params int[] indexes)
    {
        var collection = Wiring.Create();
        Assert.True(collection is not null, "Create() returned null.");
        foreach (var i in indexes) Wiring.Add(collection!, Items[i]);
        return collection!;
    }

    protected IReadOnlyList<object> EachInOrder(object collection)
    {
        var seen = Wiring.EachInOrder(collection);
        Assert.True(seen is not null,
            $"{CollectionType.Name} doesn't implement IEnumerable<{Wiring.ItemType.Name}>, so foreach can't go over it.");
        return seen!;
    }

    protected string Ids(IEnumerable<object> items) => string.Join(",", items.Select(Wiring.IdOf));

    protected string IdsAt(params int[] indexes) => string.Join(",", indexes.Select(i => Wiring.IdOf(Items[i])));

    /// <summary>Indexers, whatever the student called them ("Item" by default, or renamed with IndexerName).</summary>
    protected static PropertyInfo? Indexer(Type type, Type parameterType) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetIndexParameters() is [{ } only] && only.ParameterType == parameterType);

    /// <summary>
    /// True when the type contains a method written with <c>yield return</c>: the compiler turns one into a nested
    /// state-machine class implementing IEnumerator. Handing back another collection's enumerator leaves no such type behind.
    /// </summary>
    protected static bool HasIteratorMember(Type type) =>
        type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Any(t => t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) && typeof(IEnumerator).IsAssignableFrom(t));

    protected static T Fail<T>(string message)
    {
        Assert.Fail(message);
        return default!;   // never reached
    }
}
