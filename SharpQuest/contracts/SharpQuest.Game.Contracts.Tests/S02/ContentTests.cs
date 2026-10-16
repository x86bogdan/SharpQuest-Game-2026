namespace SharpQuest.Game.Tests.S02;

/// <summary>Session 2, required feature: "20+ things with 4+ varied attributes", loaded from your content file.</summary>
public class ContentTests : S02TestBase
{
    [Fact]
    public void Wiring_class_can_be_created()
    {
        Assert.NotNull(Wiring);
    }

    [Fact]
    public void The_content_file_is_in_your_unity_project()
    {
        var wiring = Wiring;
        Assert.False(string.IsNullOrWhiteSpace(wiring.ContentFile), "ContentFile is empty. Name your JSON file, e.g. \"enemies.json\".");

        var path = Contracts.GameFiles.ContentPath(wiring.ContentFile);
        Assert.True(File.Exists(path),
            $"There is no {wiring.ContentFile} in {Support.GameCode.Show(Contracts.GameFiles.ContentDirectory)}. " +
            "Your content lives in the Unity project, so the game and the tests read the same file.");
    }

    [Fact]
    public void There_are_at_least_twenty_items()
    {
        Assert.True(Content.Count >= MinimumItems,
            $"{Wiring.ContentFile} gives {Content.Count} item(s). The game track asks for at least {MinimumItems}: " +
            "collections and queries need something to work on.");
    }

    [Fact]
    public void Every_item_has_an_id_and_a_name()
    {
        foreach (var item in Content)
        {
            Assert.False(string.IsNullOrWhiteSpace(Wiring.IdOf(item)), $"An item has an empty Id ({Key(item)}).");
            Assert.False(string.IsNullOrWhiteSpace(Wiring.NameOf(item)), $"The item {Wiring.IdOf(item)} has an empty name.");
        }
    }

    [Fact]
    public void Ids_are_unique()
    {
        var duplicate = Content.Select(Wiring.IdOf).GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        Assert.True(duplicate is null,
            $"The Id \"{duplicate?.Key}\" is used {duplicate?.Count()} times. Ids are how everything else finds an item: one each.");
    }

    [Fact]
    public void Loading_twice_gives_the_same_items_in_the_same_order()
    {
        var first = Wiring.LoadContent().Select(Wiring.IdOf).ToList();
        var second = Wiring.LoadContent().Select(Wiring.IdOf).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Loading_twice_gives_separate_objects()
    {
        var first = Wiring.LoadContent();
        var second = Wiring.LoadContent();
        if (first.Count == 0) Assert.Fail($"{Wiring.ContentFile} gave no items.");

        Assert.True(!ReferenceEquals(first[0], second[0]),
            "Two loads handed back the very same object. If loading caches items in a static field, one game's damage " +
            "shows up in the next one. Build new items every time you parse.");
    }

    [Fact]
    public void Items_have_at_least_four_attributes_that_vary()
    {
        var items = Content;
        var identity = Properties(ItemType)
            .Where(p => items.All(i => Key(Read(p, i)) == Wiring.IdOf(i) || Key(Read(p, i)) == Wiring.NameOf(i)))
            .Select(p => p.Name)
            .ToHashSet();

        var properties = items.Select(i => i.GetType()).Distinct()
            .SelectMany(Properties)
            .GroupBy(p => p.Name).Select(g => g.First())
            .Where(p => !identity.Contains(p.Name))
            .ToList();

        var varying = properties
            .Where(p => items.Where(i => p.DeclaringType!.IsInstanceOfType(i)).Select(i => Key(Read(p, i))).Distinct().Count() >= 2)
            .Select(p => p.Name)
            .ToList();

        Assert.True(varying.Count >= MinimumAttributes,
            $"Besides the Id and the name, {varying.Count} public propert{(varying.Count == 1 ? "y varies" : "ies vary")} across your content " +
            $"({string.Join(", ", varying.DefaultIfEmpty("none"))}). The game track asks for at least {MinimumAttributes}, so the " +
            "queries in session 7 have something to ask. Public fields don't count: make them properties ({ get; }).");
    }
}
