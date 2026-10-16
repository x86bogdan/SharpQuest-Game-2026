using System.Collections;
using System.Reflection;
using SharpQuest.Game.Contracts.Grading;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S02;

/// <summary>Shared by the S02 test classes: the wiring, the content it loads, and small helpers.</summary>
public abstract class S02TestBase
{
    /// <summary>The minimum the game-track spec asks for: 20 things. A pair's 40 is checked in the room.</summary>
    protected const int MinimumItems = 20;

    /// <summary>"Four varied attributes", besides the Id and the name.</summary>
    protected const int MinimumAttributes = 4;

    private IContentWiring? _wiring;
    private IReadOnlyList<object>? _content;

    protected IContentWiring Wiring => _wiring ??= GameCode.Wiring<IContentWiring>("ContentWiring<YourItem>");

    protected Type ItemType => Wiring.ItemType;

    /// <summary>The content file, loaded once per test class instance.</summary>
    protected IReadOnlyList<object> Content
    {
        get
        {
            if (_content is not null) return _content;
            var wiring = Wiring;   // resolve first, so "not started" skips instead of failing
            IReadOnlyList<object>? loaded = null;
            var ex = Record.Exception(() => loaded = wiring.LoadContent());
            if (ex is FileNotFoundException missing)
                Assert.Fail(missing.Message);
            if (ex is not null)
                Assert.Fail($"Loading {wiring.ContentFile} threw {ex.GetType().Name}: {ex.Message}");
            Assert.True(loaded is not null, "ParseContent returned null. Return an empty list when there is nothing.");
            return _content = loaded!;
        }
    }

    /// <summary>Public readable instance properties, inherited ones included: what a tester (or Newtonsoft) can see.</summary>
    protected static IReadOnlyList<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetMethod!.IsPublic)
            .Where(p => p.Name != "EqualityContract")
            .ToList();

    /// <summary>A value as comparable text: a list compares by its elements, not by reference.</summary>
    protected static string Key(object? value) => value switch
    {
        null => "<null>",
        string s => s,
        IEnumerable e => "[" + string.Join(",", e.Cast<object?>().Select(Key)) + "]",
        _ => value.ToString() ?? "",
    };

    protected static object? Read(PropertyInfo p, object item)
    {
        try { return p.GetValue(item); }
        catch (TargetInvocationException e) { return $"<threw {e.InnerException?.GetType().Name}>"; }
    }
}
