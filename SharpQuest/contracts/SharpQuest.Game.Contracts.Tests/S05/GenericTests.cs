using System.Reflection;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S05;

/// <summary>Session 5: a generic type of your own, doing a real job: Recent&lt;T&gt;.</summary>
public class GenericTests : S05TestBase
{
    private Type Definition =>
        Wiring.GenericDefinition ?? Fail<Type>("GenericDefinition is null. Return your own generic type, e.g. typeof(RecentList<>).");

    [Fact]
    public void The_generic_type_is_yours_and_open()
    {
        var d = Definition;

        Assert.True(GameCode.IsGameCore(d), $"{d.Name} lives in {d.Assembly.GetName().Name}. Write your own generic type in Game.Core.");
        Assert.True(d.IsGenericTypeDefinition,
            $"{d.Name} is not an open generic type. Return typeof(YourType<>), with the angle brackets empty, not a closed one like typeof(YourType<string>).");
    }

    [Fact]
    public void Its_type_parameter_is_actually_used()
    {
        var d = Definition;
        Assert.True(d.GetGenericArguments().Length > 0, $"{d.Name} has no type parameter.");

        var used = d.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(m => m switch
            {
                MethodInfo method => Mentions(method.ReturnType) || method.GetParameters().Any(p => Mentions(p.ParameterType)),
                PropertyInfo property => Mentions(property.PropertyType),
                FieldInfo field => Mentions(field.FieldType),
                ConstructorInfo ctor => ctor.GetParameters().Any(p => Mentions(p.ParameterType)),
                _ => false,
            });

        Assert.True(used,
            $"Nothing in {d.Name} mentions its type parameter, so it isn't really generic. " +
            "A type parameter earns its place by appearing in a field, a parameter or a return type.");

        static bool Mentions(Type t) => t.IsGenericParameter || (t.ContainsGenericParameters && t.GetGenericArguments().Any(Mentions));
    }

    private string Pushed(int capacity, params string[] values) => string.Join(",", Wiring.Recent(capacity, values));

    [Fact]
    public void The_newest_value_comes_first()
    {
        Assert.Equal("c,b,a", Pushed(3, "a", "b", "c"));
    }

    [Fact]
    public void It_never_holds_more_than_its_capacity()
    {
        var held = string.Join(",", Wiring.Recent(2, 1, 2, 3, 4, 5));

        Assert.True(held == "5,4",
            $"After pushing 1,2,3,4,5 into one of capacity 2 it holds \"{held}\", not \"5,4\". When it's full, the oldest value drops out.");
    }

    [Fact]
    public void Pushing_something_already_there_moves_it_to_the_front()
    {
        var held = Pushed(3, "a", "b", "a");

        Assert.True(held == "a,b",
            $"After pushing a, b, a into one of capacity 3 it holds \"{held}\", not \"a,b\". A value already in there moves to the front; " +
            "it doesn't take a second place.");
    }

    [Fact]
    public void The_same_value_over_and_over_is_still_one_value()
    {
        var held = Pushed(3, "a", "a", "a");

        Assert.True(held == "a", $"Pushing a three times left \"{held}\", not \"a\".");
    }

    [Fact]
    public void A_repeat_still_counts_as_the_newest()
    {
        Assert.Equal("a,c,b", Pushed(3, "a", "b", "c", "a"));
    }

    [Fact]
    public void Nothing_pushed_means_nothing_held()
    {
        Assert.Empty(Wiring.Recent<string>(3));
    }

    [Fact]
    public void Fewer_values_than_capacity_is_fine()
    {
        Assert.Equal("2,1", string.Join(",", Wiring.Recent(5, 1, 2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void A_capacity_below_one_is_a_programming_error(int capacity)
    {
        var wiring = Wiring;

        var ex = Record.Exception(() => wiring.Recent(capacity, "a"));

        Assert.True(ex is not null, $"A capacity of {capacity} returned instead of throwing.");
        Assert.IsAssignableFrom<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void It_works_for_numbers_too()
    {
        Assert.Equal("3,1,2", string.Join(",", Wiring.Recent(3, 2, 1, 3)));
    }

    [Fact]
    public void It_works_for_your_own_items()
    {
        var first = Items[0];
        var second = Items[1];

        var recent = Wiring.Recent(2, first, second, first);

        Assert.Equal(2, recent.Count);
        Assert.Same(first, recent[0]);
        Assert.Same(second, recent[1]);
    }

    [Fact]
    public void Two_uses_do_not_share_state()
    {
        var one = string.Join(",", Wiring.Recent(2, "a", "b"));
        var two = string.Join(",", Wiring.Recent(2, "x"));

        Assert.Equal("b,a", one);
        Assert.Equal("x", two);
    }

    [Fact]
    public void Recent_really_uses_your_generic_type()
    {
        var wiring = Wiring;
        var recent = wiring.GetType().GetMethods().First(m => m.Name == "Recent" && m.DeclaringType == wiring.GetType());
        var definition = Definition;

        var uses = IlCalls.From(recent).Any(c => c.Callee.DeclaringType is { IsGenericType: true } t && t.GetGenericTypeDefinition() == definition);

        Assert.True(uses,
            $"Recent never touches {definition.Name}. GenericDefinition has to be the type that does the job: " +
            $"make a {definition.Name.Split('`')[0]}<T> with the capacity, push the values into it, and return what it holds.");
    }
}
