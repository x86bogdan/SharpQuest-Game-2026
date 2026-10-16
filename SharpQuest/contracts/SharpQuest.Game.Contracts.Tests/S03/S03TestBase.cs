using System.Text.RegularExpressions;
using SharpQuest.Game.Contracts.Grading;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S03;

/// <summary>Shared by the S03 test classes: the wiring, and your compiled tests.</summary>
public abstract class S03TestBase
{
    private ISearchWiring? _wiring;
    private IReadOnlyList<object>? _items;

    protected ISearchWiring Wiring => _wiring ??= GameCode.Wiring<ISearchWiring>("SearchWiring<YourItem>");

    /// <summary>The items to search, loaded once per test.</summary>
    protected IReadOnlyList<object> Items
    {
        get
        {
            if (_items is not null) return _items;
            var wiring = Wiring;
            var items = wiring.Items();
            Assert.True(items is { Count: >= 3 }, $"Items() gave {items?.Count ?? 0} items. Give the search at least three to choose from.");
            return _items = items!;
        }
    }

    protected IReadOnlyList<object> Search(IReadOnlyList<object> items, string query)
    {
        var found = Wiring.Search(items, query);
        Assert.True(found is not null, $"Search(items, \"{query}\") returned null. Nothing found is an empty list.");
        return found!;
    }

    protected string NameOf(object item) => Wiring.NameOf(item);

    /// <summary>The words of a name a search could use: two letters or more.</summary>
    protected static string[] WordsOf(string name) =>
        Regex.Matches(name, @"\p{L}{2,}").Select(m => m.Value).ToArray();

    /// <summary>Your compiled tests, read (never run) by the checks in YourTestsTests.</summary>
    protected TestAssemblyFacts Facts()
    {
        _ = Wiring;   // no wiring class yet = session not started = skipped
        var path = MutationResults.FindTestAssembly();
        if (path is null)
            Assert.Fail(MutationResults.HasTestFiles()
                ? "Your tests didn't build, so there is nothing to read. The first error in sq test's output says why."
                : $"There are no tests in {GameCode.Show(SharpQuest.Game.Contracts.GameFiles.TestsDirectory)}/ yet. Session 3 starts there: " +
                  "a .cs file with a class of NUnit tests ([Test], Assert.AreEqual), over your own types.");
        return TestAssemblyFacts.Load(path!);
    }

    protected MutationResults Mutation()
    {
        _ = Wiring;
        var r = MutationResults.LoadOrSkip();
        if (r.BuildFailed) Assert.Fail("Your tests didn't build. The first error in sq test's output says why.");
        return r;
    }
}
