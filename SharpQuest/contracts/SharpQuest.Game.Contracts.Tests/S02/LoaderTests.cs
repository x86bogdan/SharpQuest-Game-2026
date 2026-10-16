namespace SharpQuest.Game.Tests.S02;

/// <summary>Session 2: the loader your game calls in Unity copes with a bad file instead of crashing.</summary>
public class LoaderTests : S02TestBase
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("not json at all")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("[]")]
    public void Text_that_is_not_content_gives_an_empty_list(string json)
    {
        var wiring = Wiring;
        IReadOnlyList<object>? result = null;

        var ex = Record.Exception(() => result = wiring.ParseContent(json));

        if (ex is not null)
            Assert.Fail($"ParseContent threw {ex.GetType().Name} for {Show(json)}. A bad file should give an empty list, not crash the game.");
        Assert.True(result is not null, $"ParseContent returned null for {Show(json)}. Return an empty list.");
        Assert.True(result!.Count == 0, $"ParseContent found {result.Count} item(s) in {Show(json)}.");
    }

    [Fact]
    public void Parsing_the_file_yourself_gives_the_same_items()
    {
        var text = Contracts.GameFiles.ReadContent(Wiring.ContentFile);

        var parsed = Wiring.ParseContent(text).Select(Wiring.IdOf);

        Assert.Equal(Content.Select(Wiring.IdOf), parsed);
    }

    private static string Show(string json) => json.Trim().Length == 0 ? "blank text" : $"'{json}'";
}
