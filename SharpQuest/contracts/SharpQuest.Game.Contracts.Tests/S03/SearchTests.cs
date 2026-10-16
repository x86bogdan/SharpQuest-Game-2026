using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S03;

/// <summary>Session 3: SearchMatcher, used by your game, over your own content.</summary>
public class SearchTests : S03TestBase
{
    [Fact]
    public void Search_goes_through_SearchMatcher_from_your_own_code()
    {
        var wiring = Wiring;
        var method = wiring.GetType().GetMethods().First(m => m.Name == "Search" && m.DeclaringType == wiring.GetType());
        var calls = IlCalls.From(method);

        var matcher = calls.Where(c => c.Callee.DeclaringType?.FullName == "SharpQuest.SearchMatcher" && c.Callee.Name == "Matches").ToList();
        Assert.True(matcher.Count > 0,
            "Search never calls SearchMatcher.Matches. The rules are already written and tested (by you): create one with " +
            "SearchMatcher.Create() and ask it about each item's name.");
        Assert.True(matcher.Any(c => c.Caller.Module.Assembly == GameCode.Core),
            "SearchMatcher is called from your wiring. The search is part of your game: write it in Game.Core " +
            "(a static method taking the items and the query) and make the wiring one line that calls it.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_query_finds_everything_in_order(string query)
    {
        var items = Items;

        var found = Search(items, query);

        Assert.True(found.SequenceEqual(items),
            $"Searching for \"{query}\" gave {found.Count} of the {items.Count} items{(found.Count == items.Count ? ", in another order" : "")}. " +
            "An empty query matches everything (rule R1), and a search keeps the order it was given.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_shouted_word_from_a_name_finds_that_item(int index)
    {
        var items = Items;
        var item = items[index];
        var words = WordsOf(NameOf(item));
        Assert.True(words.Length > 0, $"'{NameOf(item)}' has no word of two or more letters to search for.");

        var query = words.OrderByDescending(w => w.Length).First().ToUpperInvariant();

        Assert.True(Search(items, query).Contains(item),
            $"Searching for '{query}' didn't find '{NameOf(item)}'. Matching must ignore case (rule R2).");
    }

    [Fact]
    public void Accents_in_the_query_are_ignored()
    {
        var items = Items;
        var item = items[0];
        var word = WordsOf(NameOf(item)).OrderByDescending(w => w.Length).FirstOrDefault();
        if (word is null) return;
        var vowel = word.IndexOfAny("aeiouAEIOU".ToCharArray());
        if (vowel < 0) return;   // nothing to accent

        var accented = word[..vowel] + Accent(word[vowel]) + word[(vowel + 1)..];

        Assert.True(Search(items, accented).Contains(item),
            $"Searching for '{accented}' didn't find '{NameOf(item)}'. Diacritics must be ignored (rule R3).");
    }

    [Fact]
    public void Word_order_in_the_query_does_not_matter()
    {
        var items = Items;
        var item = items.FirstOrDefault(i => WordsOf(NameOf(i)).Length >= 2);
        if (item is null) return;   // no name of two words to reorder

        var reversed = string.Join(" ", WordsOf(NameOf(item)).Reverse());

        Assert.True(Search(items, reversed).Contains(item),
            $"Searching for '{reversed}' didn't find '{NameOf(item)}'. Word order must not matter (rule R5).");
    }

    [Fact]
    public void A_nonsense_query_finds_nothing()
    {
        Assert.Empty(Search(Items, "zqxjv-no-such-thing"));
    }

    [Fact]
    public void All_words_must_match()
    {
        var items = Items;
        var item = items[0];
        var word = WordsOf(NameOf(item)).First();

        Assert.False(Search(items, word + " zqxjv").Contains(item),
            $"'{word} zqxjv' found '{NameOf(item)}', but 'zqxjv' isn't in it. Every word must match (rule R4).");
    }

    [Fact]
    public void What_it_finds_comes_from_what_it_was_given()
    {
        var items = Items;
        var some = items.Take(2).ToList();

        var found = Search(some, "");

        Assert.True(found.Count == 2 && found.All(f => some.Contains(f)),
            $"Searching two items for everything gave {found.Count}. Search the list you're given, not your whole content.");
    }

    private static string Accent(char c) => c switch
    {
        'a' => "á", 'e' => "é", 'i' => "í", 'o' => "ó", 'u' => "ú",
        'A' => "Á", 'E' => "É", 'I' => "Í", 'O' => "Ó", 'U' => "Ú",
        _ => c.ToString(),
    };
}
