// Session 3 - Testing & the contract
// Two things this session:
//   1. Tests of your own: NUnit tests in Assets/Game/Tests/Core/, over your own types. They run in Unity's
//      Test Runner (Window > General > Test Runner > EditMode) and in `sq test`, unchanged.
//   2. SharpQuest.SearchMatcher (in Assets/Game/Core/SharpQuest/, put there by sq): a small, fully specified
//      search rule. YOU write the tests for it. `sq test` then runs them against eight subtly broken versions.
//      Good tests catch them; weak tests don't. Then use it: a search over your own content.
//
// Nothing from session 2 may break: its checks run again every time.

using SharpQuest.Game.Contracts.Grading;

namespace SharpQuest.Game.Contracts
{
    /// <summary>
    /// Tells the S03 tests how your game searches its content. Write one class deriving from this in
    /// SharpQuest/Wiring/, naming YOUR item type:
    /// <code>
    /// public sealed class Search : SearchWiring&lt;Enemy&gt;
    /// {
    ///     public override IReadOnlyList&lt;Enemy&gt; Items() =&gt; new Content().LoadContent();
    ///     public override string NameOf(Enemy item) =&gt; item.Name;
    ///     public override IReadOnlyList&lt;Enemy&gt; Search(IReadOnlyList&lt;Enemy&gt; items, string query) =&gt; Codex.Search(items, query);
    /// }
    /// </code>
    ///
    /// <para>What the S03 tests read in your test assembly (nothing in it is executed by them; sq runs it):</para>
    /// <list type="bullet">
    /// <item>at least 5 tests that use your own types in Game.Core (not the wiring, not SearchMatcher);</item>
    /// <item>one of them checks that your code refuses bad input: <c>Assert.Throws</c>, <c>Assert.Catch</c> or <c>Throws.…</c>;</item>
    /// <item>a <c>[TestCase]</c> test with two or more rows (or a <c>[TestCaseSource]</c>);</item>
    /// <item>at least 4 tests of SearchMatcher, in a class whose name contains "SearchMatcher", each creating
    ///       it with <c>SearchMatcher.Create()</c>, and not trying to detect which version is running;</item>
    /// <item>all of them pass.</item>
    /// </list>
    /// </summary>
    public abstract class SearchWiring<TItem> : ISearchWiring where TItem : class
    {
        /// <summary>At least three items to search. Your content is the natural source.</summary>
        public abstract IReadOnlyList<TItem> Items();

        /// <summary>The name a player searches for: what <see cref="Search"/> matches against.</summary>
        public abstract string NameOf(TItem item);

        /// <summary>
        /// The items whose name matches <paramref name="query"/> according to <c>SearchMatcher</c>, in the order
        /// given. You may match more than the name. The method lives in Game.Core and creates the matcher with
        /// <c>SearchMatcher.Create()</c>; it never changes the list it's given.
        /// </summary>
        public abstract IReadOnlyList<TItem> Search(IReadOnlyList<TItem> items, string query);

        string IWiring.BaseName => "SearchWiring<YourItem>";
        Type ISearchWiring.ItemType => typeof(TItem);
        IReadOnlyList<object> ISearchWiring.Items() => Items();
        string ISearchWiring.NameOf(object item) => NameOf((TItem)item);
        IReadOnlyList<object> ISearchWiring.Search(IReadOnlyList<object> items, string query) => Search(items.Cast<TItem>().ToList(), query);
    }
}

namespace SharpQuest.Game.Contracts.Grading
{
    /// <summary>The tests' view of <see cref="SearchWiring{TItem}"/>. You don't implement this: the base class does.</summary>
    public interface ISearchWiring : IWiring
    {
        Type ItemType { get; }
        IReadOnlyList<object> Items();
        string NameOf(object item);
        IReadOnlyList<object> Search(IReadOnlyList<object> items, string query);
    }
}
