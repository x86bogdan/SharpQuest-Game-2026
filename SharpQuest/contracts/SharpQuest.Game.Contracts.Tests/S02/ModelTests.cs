using System.Text.Json;
using SharpQuest.Game.Contracts;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S02;

/// <summary>
/// Session 2, required feature: "3+ types, an enum, an interface, a record, zero nullable warnings" - all in Game.Core.
/// Zero nullable warnings is enforced by the build itself (Directory.Build.targets), not here.
/// </summary>
public class ModelTests : S02TestBase
{
    [Fact]
    public void Items_are_your_own_type_in_game_core()
    {
        Assert.True(GameCode.IsGameCore(ItemType),
            $"Your content's item type is {ItemType.FullName}, from {ItemType.Assembly.GetName().Name}. It has to be a type of " +
            "your own in Assets/Game/Core/.");
        Assert.True(ItemType.IsClass, $"{ItemType.Name} is not a class.");
    }

    [Fact]
    public void Game_core_has_at_least_three_types_of_your_own()
    {
        Started();
        var types = GameCode.ModelTypes();

        Assert.True(types.Count >= 3, $"Found {types.Count} type(s) in Game.Core ({Names(types)}). Session 2 asks for at least 3.");
    }

    [Fact]
    public void Game_core_has_an_enum()
    {
        Started();
        Assert.True(GameCode.ModelTypes().Any(t => t.IsEnum),
            "Game.Core has no enum. Which attribute of your content has a fixed set of values? A damage type, a rarity, a biome.");
    }

    [Fact]
    public void Game_core_has_an_interface_that_something_implements()
    {
        Started();
        var model = GameCode.ModelTypes();
        var interfaces = model.Where(t => t.IsInterface).ToList();

        Assert.True(interfaces.Count >= 1, "Game.Core declares no interface of its own.");

        var used = interfaces.Where(i => model.Any(t => !t.IsInterface && i.IsAssignableFrom(t))).ToList();
        Assert.True(used.Count >= 1,
            $"Nothing in Game.Core implements {string.Join(" or ", interfaces.Select(i => i.Name))}. " +
            "An interface earns its place when two different things in your game can be used the same way.");
    }

    [Fact]
    public void Game_core_has_a_record()
    {
        Started();
        Assert.True(GameCode.ModelTypes().Any(GameCode.IsRecord),
            "Game.Core has no record. Stats, a cost, a position on the grid: values that belong together and compare by value.");
    }

    [Fact]
    public void Game_core_cannot_use_unity()
    {
        Started();
        if (!string.Equals(GameFiles.Engine, "Unity", StringComparison.OrdinalIgnoreCase)) return;

        var asmdef = Path.Combine(GameFiles.CoreDirectory, "Game.Core.asmdef");
        Assert.True(File.Exists(asmdef),
            $"There is no {GameCode.Show(asmdef)}. Run sq init: it puts it there, and Unity then compiles your rules as their own assembly.");

        using var doc = JsonDocument.Parse(File.ReadAllText(asmdef), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var noEngine = doc.RootElement.TryGetProperty("noEngineReferences", out var value) && value.ValueKind == JsonValueKind.True;

        Assert.True(noEngine,
            $"{GameCode.Show(asmdef)} lets Game.Core use UnityEngine. Tick \"No Engine References\" in its Inspector " +
            "(or set \"noEngineReferences\": true): your rules have to run without Unity, or nothing can test them.");
    }

    // The shape checks don't need the wiring, but a session without one counts as not started.
    private void Started() => _ = Wiring;

    private static string Names(IEnumerable<Type> types) => string.Join(", ", types.Select(t => t.Name).DefaultIfEmpty("none"));
}
