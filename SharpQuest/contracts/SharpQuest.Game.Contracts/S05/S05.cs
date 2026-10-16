// Session 5 - Collections & generics
// Something in your game becomes a collection of your own: foreach works on it, [0] and ["id"] work on it,
// and it has an iterator you wrote. A party, an inventory, a wave, a deck, the rooms of a building.
// Then one generic type of your own, so `T` stops being something other people write.
//
// Nothing from sessions 2-4 may break: their checks run again every time.

using SharpQuest.Game.Contracts.Grading;

namespace SharpQuest.Game.Contracts
{
    /// <summary>
    /// Tells the S05 tests about your collection and your generic type. Write one class deriving from this in
    /// SharpQuest/Wiring/, naming YOUR collection and what it holds:
    /// <code>
    /// public sealed class Collections : CollectionWiring&lt;Spawner, Enemy&gt;
    /// {
    ///     public override IReadOnlyList&lt;Enemy&gt; Items() =&gt; new Content().LoadContent();
    ///     public override Spawner Create() =&gt; new Spawner();
    ///     public override void Add(Spawner spawner, Enemy enemy) =&gt; spawner.Add(enemy);
    ///     public override string IdOf(Enemy enemy) =&gt; enemy.Id;
    ///     public override string NameOf(Enemy enemy) =&gt; enemy.Name;
    ///     public override Enemy At(Spawner spawner, int index) =&gt; spawner[index];
    ///     public override Enemy? ById(Spawner spawner, string id) =&gt; spawner[id];
    ///     public override IEnumerable&lt;Enemy&gt; Matching(Spawner spawner, string text) =&gt; spawner.Matching(text);
    ///     public override Type GenericDefinition =&gt; typeof(RecentList&lt;&gt;);
    ///     public override IReadOnlyList&lt;T&gt; Recent&lt;T&gt;(int capacity, params T[] pushed) =&gt; RecentList&lt;T&gt;.Of(capacity, pushed);
    /// }
    /// </code>
    ///
    /// <para>What the tests expect of your collection (<typeparamref name="TCollection"/>, in Game.Core):</para>
    /// <list type="bullet">
    /// <item>it implements <c>IEnumerable&lt;TItem&gt;</c>, so <c>foreach (var enemy in spawner)</c> works on the collection itself;</item>
    /// <item>at least one of its members is written with <c>yield return</c>: an iterator you wrote, not a list's enumerator handed back;</item>
    /// <item>it has an indexer by position, <c>this[int]</c>, and one by id, <c>this[string]</c>.</item>
    /// </list>
    /// <para>And of your generic type: one type of your own with a type parameter, doing a real job.
    /// <see cref="Recent{T}"/> below is that job: see its rules.</para>
    /// </summary>
    public abstract class CollectionWiring<TCollection, TItem> : ICollectionWiring where TCollection : class where TItem : class
    {
        /// <summary>At least four different items to put in the collection. Your content is fine.</summary>
        public abstract IReadOnlyList<TItem> Items();

        /// <summary>A new, empty collection.</summary>
        public abstract TCollection Create();

        /// <summary>Adds an item, at the end. Adding an item whose Id is already there is your call (refuse, or replace).</summary>
        public abstract void Add(TCollection collection, TItem item);

        /// <summary>The item's Id, as in S02.</summary>
        public abstract string IdOf(TItem item);

        /// <summary>The item's name, as in S02.</summary>
        public abstract string NameOf(TItem item);

        /// <summary>The item at <paramref name="index"/>, through your <c>this[int]</c> indexer, in the order items were added.
        /// Out of range throws <see cref="ArgumentOutOfRangeException"/>.</summary>
        public abstract TItem At(TCollection collection, int index);

        /// <summary>The item with this Id, through your <c>this[string]</c> indexer, or null if there is none.</summary>
        public abstract TItem? ById(TCollection collection, string id);

        /// <summary>
        /// The items whose name contains <paramref name="text"/> (ignoring case), in collection order, written as an
        /// iterator with <c>yield return</c>, so nothing is looked at until somebody enumerates it.
        /// </summary>
        public abstract IEnumerable<TItem> Matching(TCollection collection, string text);

        /// <summary>
        /// Your own generic type, as an open definition: <c>typeof(RecentList&lt;&gt;)</c>. A type you wrote, in Game.Core,
        /// with at least one type parameter, and what <see cref="Recent{T}"/> actually uses.
        /// </summary>
        public abstract Type GenericDefinition { get; }

        /// <summary>
        /// Push each value of <paramref name="pushed"/>, in order, into an instance of your generic type with this
        /// <paramref name="capacity"/>, then return what it holds. The last few targets a tower picked, recently used items,
        /// the rooms an animatronic visited:
        /// <list type="number">
        /// <item><b>Newest first.</b> The last value pushed comes first.</item>
        /// <item><b>Bounded.</b> It never holds more than <paramref name="capacity"/> values; the oldest drops out.</item>
        /// <item><b>No duplicates.</b> Pushing a value that is already in there moves it to the front instead
        ///       of storing it twice (use <c>EqualityComparer&lt;T&gt;.Default</c>).</item>
        /// <item>A capacity below 1 is a programming error: <see cref="ArgumentOutOfRangeException"/>.</item>
        /// </list>
        /// The tests call this with several different <c>T</c>s, so <c>object</c> in a trench coat won't do.
        /// </summary>
        public abstract IReadOnlyList<T> Recent<T>(int capacity, params T[] pushed);

        string IWiring.BaseName => "CollectionWiring<YourCollection, YourItem>";
        Type ICollectionWiring.CollectionType => typeof(TCollection);
        Type ICollectionWiring.ItemType => typeof(TItem);
        IReadOnlyList<object> ICollectionWiring.Items() => Items();
        object ICollectionWiring.Create() => Create();
        void ICollectionWiring.Add(object collection, object item) => Add((TCollection)collection, (TItem)item);
        string ICollectionWiring.IdOf(object item) => IdOf((TItem)item);
        string ICollectionWiring.NameOf(object item) => NameOf((TItem)item);
        object ICollectionWiring.At(object collection, int index) => At((TCollection)collection, index);
        object? ICollectionWiring.ById(object collection, string id) => ById((TCollection)collection, id);
        IEnumerable<object> ICollectionWiring.Matching(object collection, string text) => Matching((TCollection)collection, text);
        IReadOnlyList<object>? ICollectionWiring.EachInOrder(object collection)
        {
            // foreach over the collection itself, exactly as your game's code would write it.
            if (collection is not IEnumerable<TItem> items) return null;
            var seen = new List<object>();
            foreach (var item in items) seen.Add(item);
            return seen;
        }
    }
}

namespace SharpQuest.Game.Contracts.Grading
{
    /// <summary>The tests' view of <see cref="CollectionWiring{TCollection, TItem}"/>. You don't implement this: the base class does.</summary>
    public interface ICollectionWiring : IWiring
    {
        Type CollectionType { get; }
        Type ItemType { get; }
        Type GenericDefinition { get; }
        IReadOnlyList<object> Items();
        object Create();
        void Add(object collection, object item);
        string IdOf(object item);
        string NameOf(object item);
        object At(object collection, int index);
        object? ById(object collection, string id);
        IEnumerable<object> Matching(object collection, string text);
        /// <summary>null when the collection isn't an IEnumerable of its items.</summary>
        IReadOnlyList<object>? EachInOrder(object collection);
        IReadOnlyList<T> Recent<T>(int capacity, params T[] pushed);
    }
}
