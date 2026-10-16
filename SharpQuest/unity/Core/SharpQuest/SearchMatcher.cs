// MANAGED FILE: sq init and sq test put it here, and replace it when the course updates it. Don't edit it.
//
// Session 3 - your tests, graded by how much they notice.
// SearchMatcher is a small, fully specified search rule, given to you. YOU write the tests for it, in
// Assets/Game/Tests/Core/, in a class whose name contains "SearchMatcher". `sq test` then runs those tests
// against eight subtly broken versions ("mutants"). Good tests catch them; weak tests don't.
// Use it in your game too: it's a perfectly good search for an inventory, a level list or a codex.

using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SharpQuest
{
    /// <summary>
    /// Decides whether a text (an item's name, usually) matches a search query.
    /// The rules below are the whole specification. Write at least one test per rule.
    /// <list type="number">
    /// <item><b>R1</b> An empty or whitespace-only query matches every non-null text.</item>
    /// <item><b>R2</b> Matching ignores case: "GOB" matches "Goblin".</item>
    /// <item><b>R3</b> Matching ignores diacritics, in both directions: "cafe" matches "Café", "șosea" matches "Sosea".</item>
    /// <item><b>R4</b> The query is split into words on whitespace. EVERY word must occur in the text.</item>
    /// <item><b>R5</b> Word order doesn't matter: "drake fire" matches "Fire Drake".</item>
    /// <item><b>R6</b> A word may occur anywhere, even inside a longer word: "lin" matches "Goblin".</item>
    /// <item><b>R7</b> A null text never matches (the result is false, not an exception).</item>
    /// <item><b>R8</b> A null query is a programming error: <see cref="ArgumentNullException"/>.</item>
    /// </list>
    /// </summary>
    public sealed class SearchMatcher
    {
        /// <summary>
        /// The matcher to use. Normally the correct one.
        /// While grading, the SQ_MUTANT environment variable swaps in a deliberately broken one.
        /// Don't read that variable in your tests: the grader checks for it.
        /// </summary>
        public static SearchMatcher Create() =>
            new SearchMatcher(int.TryParse(Environment.GetEnvironmentVariable("SQ_MUTANT"), out var m) && m >= 1 && m <= MutantCount ? m : 0);

        /// <summary>How many broken versions the grader tries.</summary>
        public const int MutantCount = 8;

        private readonly int _mutant;

        private SearchMatcher(int mutant) => _mutant = mutant;

        public bool Matches(string? text, string query)
        {
            if (query is null)
            {
                if (_mutant == 8) return text is not null;                                       // M8: breaks R8
                throw new ArgumentNullException(nameof(query));
            }
            if (text is null)
            {
                if (_mutant == 7) throw new NullReferenceException();                             // M7: breaks R7
                return false;
            }

            var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return _mutant != 4;                                           // M4: breaks R1

            var haystack = Normalise(text);
            var needles = words.Select(Normalise).ToList();

            if (_mutant == 1)                                                                     // M1: breaks R2
            {
                haystack = StripDiacritics(text);
                needles = words.Select(StripDiacritics).ToList();
            }
            if (_mutant == 2)                                                                     // M2: breaks R3
            {
                haystack = text.ToLowerInvariant();
                needles = words.Select(w => w.ToLowerInvariant()).ToList();
            }

            if (_mutant == 3) return needles.Any(n => haystack.IndexOf(n, StringComparison.Ordinal) >= 0);   // M3: breaks R4

            if (_mutant == 5)                                                                     // M5: breaks R5
            {
                var from = 0;
                foreach (var n in needles)
                {
                    var at = haystack.IndexOf(n, from, StringComparison.Ordinal);
                    if (at < 0) return false;
                    from = at + n.Length;
                }
                return true;
            }

            if (_mutant == 6)                                                                     // M6: breaks R6
            {
                var textWords = haystack.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                return needles.All(n => textWords.Any(t => t.StartsWith(n, StringComparison.Ordinal)));
            }

            return needles.All(n => haystack.IndexOf(n, StringComparison.Ordinal) >= 0);
        }

        private static string Normalise(string s) => StripDiacritics(s).ToLowerInvariant();

        private static string StripDiacritics(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
