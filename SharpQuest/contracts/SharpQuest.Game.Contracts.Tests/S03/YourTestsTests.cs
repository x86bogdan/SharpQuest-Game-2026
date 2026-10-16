using SharpQuest.Game.Contracts;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S03;

/// <summary>
/// Session 3: tests of your own, over your own rules. These checks read your compiled tests (Assets/Game/Tests/Core/);
/// nothing in them is executed here. sq runs them, and Unity's Test Runner runs the very same files.
/// </summary>
public class YourTestsTests : S03TestBase
{
    [Fact]
    public void At_least_five_tests_exercise_your_own_code()
    {
        var own = Facts().Tests.Where(t => t.UsesOwnCode).ToList();
        Assert.True(own.Count >= 5,
            $"Found {own.Count} test(s) that use your own types ({string.Join(", ", own)}). Session 3 asks for at least 5. " +
            "Tests that only test SearchMatcher don't count here: test the rules of YOUR game (damage, movement, what a room allows).");
    }

    [Fact]
    public void At_least_one_test_checks_that_your_code_refuses_bad_input()
    {
        Assert.True(Facts().Tests.Any(t => t.UsesOwnCode && t.ChecksException),
            "None of your own-code tests checks for an exception (Assert.Throws<T>(() => ...), Assert.Catch, or Assert.That(..., Throws.…)). " +
            "Test at least one rule your types enforce: an enemy without an Id, a negative amount of damage, a door that doesn't exist.");
    }

    [Fact]
    public void At_least_one_test_runs_several_cases()
    {
        Assert.True(Facts().Tests.Any(t => t.IsParameterised),
            "No test with two or more [TestCase(...)] rows (or a [TestCaseSource]). Use one where the same check applies to " +
            "several inputs: each damage type, each kind, each door.");
    }

    [Fact]
    public void There_are_SearchMatcher_tests()
    {
        var spec = Facts().Tests.Where(t => t.Class.Contains("SearchMatcher", StringComparison.OrdinalIgnoreCase) && t.UsesSearchMatcher).ToList();
        Assert.True(spec.Count >= 4,
            $"Found {spec.Count} SearchMatcher test method(s). Write at least 4, in a class whose name contains 'SearchMatcher', " +
            "each creating the matcher with SearchMatcher.Create(). One per rule (R1-R8) is the way to catch all eight broken versions.");
    }

    [Fact]
    public void SearchMatcher_tests_do_not_peek_at_the_grader()
    {
        var peeking = Facts().Peeking.Distinct().ToList();
        Assert.True(peeking.Count == 0,
            "Your SearchMatcher tests must check behaviour, not detect which version is running: " + string.Join("; ", peeking));
    }

    [Fact]
    public void Your_tests_also_run_in_Unity()
    {
        _ = Wiring;
        if (GameFiles.Engine != "Unity") return;   // another engine has its own test runner

        var asmdef = Path.Combine(GameFiles.TestsDirectory, "Game.Core.Tests.asmdef");
        Assert.True(File.Exists(asmdef),
            $"There's no {GameCode.Show(asmdef)}, so Unity's Test Runner won't find your tests. Run sq init: it puts one there.");
        var text = File.ReadAllText(asmdef);
        Assert.True(text.Contains("\"Game.Core\"") && text.Contains("nunit.framework.dll") && text.Contains("UNITY_INCLUDE_TESTS"),
            $"{GameCode.Show(asmdef)} has been changed: it must reference Game.Core and nunit.framework.dll, with the UNITY_INCLUDE_TESTS " +
            "constraint, or Unity's Test Runner won't run what sq test runs. Delete it and run sq init to get a fresh one.");
    }
}
