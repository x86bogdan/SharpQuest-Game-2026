using System.Reflection;
using SharpQuest.Game.Contracts.Grading;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S06;

/// <summary>Session 6: functions as parameters - the caller brings the rule, your code in Game.Core runs it.</summary>
public class FunctionTests
{
    private IFunctionWiring? _wiring;
    private IReadOnlyList<object>? _items;

    private IFunctionWiring Wiring => _wiring ??= GameCode.Wiring<IFunctionWiring>("FunctionWiring<YourItem>");

    private IReadOnlyList<object> Items
    {
        get
        {
            if (_items is not null) return _items;
            var items = Wiring.Items();
            Assert.True(items is not null && items.Count >= 3, $"Items() gave {items?.Count ?? 0} item(s). Give the tests at least three.");
            return _items = items!;
        }
    }

    [Fact]
    public void Wiring_class_can_be_created()
    {
        Assert.NotNull(Wiring);
    }

    [Fact]
    public void Game_core_has_a_method_that_takes_a_function()
    {
        _ = Wiring;
        var takers = GameCode.ModelTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .Where(m => m.GetParameters().Any(p => typeof(Delegate).IsAssignableFrom(p.ParameterType)))
            .ToList();

        Assert.True(takers.Count > 0,
            "No public method in Game.Core takes a function (a Func<...>, an Action<...>, a Predicate<...>). Your Where has to be " +
            "your own code in Game.Core: the wiring only calls it.");
    }

    [Fact]
    public void Where_keeps_what_the_function_says_yes_to()
    {
        var wanted = Items[1];

        var kept = Wiring.Where(Items, item => ReferenceEquals(item, wanted));

        Assert.Same(wanted, Assert.Single(kept));
    }

    [Fact]
    public void A_function_that_always_says_no_keeps_nothing()
    {
        Assert.Empty(Wiring.Where(Items, _ => false));
    }

    [Fact]
    public void A_function_that_always_says_yes_keeps_everything_in_order()
    {
        var kept = Wiring.Where(Items, _ => true);

        Assert.True(kept.Count == Items.Count && kept.Zip(Items).All(p => ReferenceEquals(p.First, p.Second)),
            "With a function that says yes to everything, Where should give back every item, in the order given. Don't sort or filter on your own.");
    }

    [Fact]
    public void The_function_is_asked_about_every_item_exactly_once()
    {
        var asked = 0;

        Wiring.Where(Items, _ => { asked++; return true; });

        Assert.True(asked == Items.Count, $"The function was called {asked} times for {Items.Count} items. Call the caller's rule once per item.");
    }

    [Fact]
    public void Nothing_in_is_nothing_out_and_nobody_is_asked()
    {
        var asked = 0;

        var kept = Wiring.Where([], _ => { asked++; return true; });

        Assert.Empty(kept);
        Assert.Equal(0, asked);
    }

    [Fact]
    public void A_function_can_carry_state_from_where_it_was_written()
    {
        var limit = 2;
        var seen = 0;

        var kept = Wiring.Where(Items, _ => ++seen <= limit);

        Assert.True(kept.Count == limit, "A lambda keeps a live link to the variables around it: this one keeps the first two items.");
    }

    [Fact]
    public void A_missing_function_is_a_programming_error()
    {
        var wiring = Wiring;
        var items = Items;

        var ex = Record.Exception(() => wiring.Where(items, null));

        Assert.True(ex is not null, "Where(items, null) returned instead of throwing.");
        Assert.IsAssignableFrom<ArgumentNullException>(ex);
    }
}
