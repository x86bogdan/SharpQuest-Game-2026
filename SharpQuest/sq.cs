// sq — the SharpQuest helper, game track.
//
//   sq init            set up your Unity project for the course (folders, assembly definitions, the JSON package)
//   sq init <folder>   the same, for a Unity project somewhere else (it's remembered in SharpQuest/game.props)
//   sq test            build your game's rules and your tests outside Unity, run every check, show a summary per session
//   sq test 04         only session 4's checks
//   sq update          fetch the latest contracts (do this at the start of each session)
//   sq report          show the last results again
//   sq help
//
// Run it from the top of your repository: `sq test` on Windows, `sh sq.sh test` on Linux and macOS.
// MANAGED FILE: `sq update` and CI replace it with the official copy.
//
// Same family as the labs' sq.cs; what differs: init, the paths (a Unity project next to SharpQuest/),
// the build hints, and no reference solutions, catch-up kits, mutation step or upload.

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

Console.OutputEncoding = Encoding.UTF8;

var root = Repo.FindRoot();
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var rest = args.Skip(1).ToArray();

return command switch
{
    "init" => Commands.Init(root, rest),
    "test" => Commands.Test(root, rest),
    "update" => await Commands.Update(root),
    "report" => Commands.ReportOnly(root, rest),
    _ => Commands.Help(),
};

/// <summary>Where everything is. <c>Sq</c> is the SharpQuest/ folder; the repository is the folder above it.</summary>
sealed record Paths(string Sq, string Repo, string Project, string Engine, string Core, string Unity, string Tests, string Content)
{
    public bool IsUnity => Engine.Equals("Unity", StringComparison.OrdinalIgnoreCase);

    public static Paths Read(string sq)
    {
        var props = Path.Combine(sq, "game.props");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(props))
            foreach (var e in XDocument.Load(props).Descendants())
                if (!e.HasElements && e.Parent?.Name.LocalName == "PropertyGroup" && e.Value.Trim().Length > 0)
                    values[e.Name.LocalName] = e.Value.Trim();

        string Get(string name, string fallback) => values.TryGetValue(name, out var v) ? v : fallback;
        var project = Path.GetFullPath(Path.Combine(sq, Get("GameProject", "../Game")));
        return new Paths(
            sq,
            Path.GetFullPath(Path.Combine(sq, "..")),
            project,
            Get("GameEngine", "Unity"),
            Path.GetFullPath(Path.Combine(project, Get("GameCore", "Assets/Game/Core"))),
            Path.GetFullPath(Path.Combine(project, "Assets/Game/Unity")),
            Path.GetFullPath(Path.Combine(project, Get("GameTests", "Assets/Game/Tests/Core"))),
            Path.GetFullPath(Path.Combine(project, Get("GameContent", "Assets/Game/Content"))));
    }

    /// <summary>A path as the student sees it: relative to the top of the repository, with forward slashes.</summary>
    public string Show(string path) => Path.GetRelativePath(Repo, path).Replace('\\', '/');
}

static class Commands
{
    public static int Help()
    {
        Console.WriteLine("""
            sq — SharpQuest helper (game track)

              sq init          set up your Unity project for the course (run it once, and again after sq update says so)
              sq test          build your game's rules and tests outside Unity, then show a summary per session
              sq test 04       only session 4's checks
              sq update        get the latest contracts from the course (at the start of each session)
              sq report        show the last results again

            Your rules:        <Unity project>/Assets/Game/Core/        (no UnityEngine here: see README.md)
            Your scripts:      <Unity project>/Assets/Game/Unity/       (MonoBehaviours: draw, play sounds, read input)
            Your tests:        <Unity project>/Assets/Game/Tests/Core/  (NUnit; they also run in Unity's Test Runner)
            Your content:      <Unity project>/Assets/Game/Content/     (JSON: the game and the tests read the same files)
            Your wiring:       SharpQuest/Wiring/                        (one small class per session)
            Contract tests:    SharpQuest/contracts/                     (managed: read them, don't edit them)
            """);
        return 0;
    }

    // ---------------------------------------------------------------- sq init

    public static int Init(string sq, string[] options)
    {
        var propsPath = Path.Combine(sq, "game.props");
        if (options.FirstOrDefault(o => !o.StartsWith("--")) is { } folder)
        {
            var full = Path.GetFullPath(folder);
            var relative = Path.GetRelativePath(sq, full).Replace('\\', '/');
            WriteGameProject(propsPath, relative);
            Console.WriteLine($"SharpQuest/game.props now points at {relative}");
        }

        var paths = Paths.Read(sq);
        if (paths.IsUnity && !IsUnityProject(paths.Project))
        {
            Console.WriteLine($"There's no Unity project at {paths.Show(paths.Project)} yet.");
            Console.WriteLine();
            Console.WriteLine("  1. Unity Hub → New project → pick the course's Unity version and a 2D or 3D template.");
            Console.WriteLine($"  2. Location: {paths.Repo}");
            Console.WriteLine($"     Project name: {Path.GetFileName(paths.Project)}");
            Console.WriteLine("  3. Create it, close Unity once it has opened, and run sq init again.");
            Console.WriteLine();
            Console.WriteLine("Already have a Unity project somewhere else in this repository? sq init <its folder>");
            return 1;
        }

        Console.WriteLine($"Setting up {paths.Show(paths.Project)} ({paths.Engine})");
        var report = new List<string>();
        foreach (var dir in new[] { paths.Core, paths.Unity, paths.Tests, paths.Content })
            if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); report.Add($"  made     {paths.Show(dir)}/"); }

        var unity = Path.Combine(sq, "unity");
        if (paths.IsUnity && Directory.Exists(unity))
        {
            foreach (var (from, to, managed) in UnityFiles(paths, unity))
            {
                var exists = File.Exists(to);
                if (exists && (!managed || File.ReadAllText(from) == File.ReadAllText(to))) { if (!managed) report.Add($"  kept     {paths.Show(to)}"); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to, overwrite: true);
                report.Add($"  {(exists ? "updated" : "added  ")}  {paths.Show(to)}");
            }
            report.AddRange(AddNewtonsoft(paths));
        }
        else if (!paths.IsUnity)
        {
            // Not Unity: only the managed polyfill, so records compile wherever the engine's compiler is C# 9.
            var polyfill = Path.Combine(unity, "Core", "SharpQuest", "IsExternalInit.cs");
            var to = Path.Combine(paths.Core, "SharpQuest", "IsExternalInit.cs");
            if (File.Exists(polyfill) && !File.Exists(to)) { Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(polyfill, to); report.Add($"  added    {paths.Show(to)}"); }
        }

        foreach (var line in report) Console.WriteLine(line);
        foreach (var warning in Checks.Setup(paths)) Console.WriteLine($"  ! {warning}");

        if (paths.IsUnity && Checks.UnityVersion(paths) is { } version)
        {
            var wanted = (string?)JsonNode.Parse(File.ReadAllText(Path.Combine(sq, "sharpquest.json")))?["unityVersion"];
            Console.WriteLine($"  Unity {version}" + (wanted is { Length: > 0 } && wanted != version ? $"   ! the course uses {wanted}: open the project with that version in Unity Hub" : ""));
        }

        Console.WriteLine();
        Console.WriteLine("Next: open the project in Unity and let it import (the JSON package downloads the first time).");
        Console.WriteLine("Then write your first class in Assets/Game/Core/, and run sq test.");
        return 0;
    }

    static bool IsUnityProject(string dir) =>
        Directory.Exists(Path.Combine(dir, "Assets")) && Directory.Exists(Path.Combine(dir, "ProjectSettings"));

    static void WriteGameProject(string propsPath, string relative)
    {
        XDocument doc;
        if (File.Exists(propsPath)) doc = XDocument.Load(propsPath, LoadOptions.PreserveWhitespace);
        else doc = new XDocument(new XElement("Project", new XElement("PropertyGroup")));
        var group = doc.Root!.Element("PropertyGroup") ?? new XElement("PropertyGroup");
        if (group.Parent is null) doc.Root.Add(group);
        var element = group.Element("GameProject");
        if (element is null) group.Add(new XElement("GameProject", relative));
        else element.Value = relative;
        doc.Save(propsPath);
    }

    /// <summary>
    /// What sq init copies from SharpQuest/unity/ into the Unity project. Only the files under Core/SharpQuest/
    /// are managed (always refreshed); everything else is created once and is the student's from then on.
    /// </summary>
    public static IEnumerable<(string From, string To, bool Managed)> UnityFiles(Paths paths, string unity)
    {
        var roles = new (string Folder, string Target)[]
        {
            ("Core", paths.Core), ("Unity", paths.Unity), ("Tests/Core", paths.Tests), ("Content", paths.Content), ("Project", paths.Project),
        };
        foreach (var (folder, target) in roles)
        {
            var source = Path.Combine(unity, folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(source)) continue;
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(source, file);
                var managed = folder == "Core" && rel.Split(Path.DirectorySeparatorChar)[0] == "SharpQuest";
                yield return (file, Path.Combine(target, rel), managed);
            }
        }
    }

    /// <summary>Game.Core reads JSON with Newtonsoft.Json; in Unity that's the com.unity.nuget.newtonsoft-json package.</summary>
    static IEnumerable<string> AddNewtonsoft(Paths paths)
    {
        const string package = "com.unity.nuget.newtonsoft-json", version = "3.2.1";
        var manifest = Path.Combine(paths.Project, "Packages", "manifest.json");
        if (!File.Exists(manifest)) { yield return $"  ! no {paths.Show(manifest)}: add the package {package} {version} in Unity's Package Manager"; yield break; }
        var json = JsonNode.Parse(File.ReadAllText(manifest))!;
        var deps = json["dependencies"]?.AsObject();
        if (deps is null) { yield return $"  ! {paths.Show(manifest)} has no dependencies list: add {package} {version} in the Package Manager"; yield break; }
        if (deps.ContainsKey(package)) yield break;
        deps[package] = version;
        File.WriteAllText(manifest, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        yield return $"  added    {package} {version} to {paths.Show(manifest)}";
    }

    // ---------------------------------------------------------------- sq test

    /// <summary>What `sq test` builds and runs: the course's tests (through your wiring) and your own tests. Never Unity.</summary>
    static readonly string ContractTests = Path.Combine("contracts", "SharpQuest.Game.Contracts.Tests", "SharpQuest.Game.Contracts.Tests.csproj");
    static readonly string OwnTests = Path.Combine("build", "Tests", "Game.Tests.Build.csproj");
    static readonly string CoreBuild = Path.Combine("build", "Core", "Game.Core.Build.csproj");

    public static int Test(string sq, string[] options)
    {
        var ci = options.Contains("--ci");
        var session = options.FirstOrDefault(o => Regex.IsMatch(o, @"^\d{1,2}$"));
        var paths = Paths.Read(sq);
        var resultsDir = Path.Combine(sq, ".sq", "results");
        if (Directory.Exists(resultsDir)) Directory.Delete(resultsDir, recursive: true);

        if (!Directory.Exists(paths.Core))
        {
            Console.WriteLine($"There's no {paths.Show(paths.Core)}/ yet. Run sq init first.");
            if (ci) Report.WriteCi(paths, [], buildFailed: true);
            return 1;
        }
        foreach (var warning in Checks.Setup(paths)) Console.WriteLine($"! {warning}");
        RefreshManaged(paths, quiet: true);

        var projects = new List<string> { ContractTests };
        if (Directory.Exists(paths.Tests) && Directory.EnumerateFiles(paths.Tests, "*.cs", SearchOption.AllDirectories).Any())
            projects.Add(OwnTests);
        else
            Console.WriteLine($"(No tests of your own in {paths.Show(paths.Tests)}/ yet: from session 3 on, that's where they go.)");

        var exitCode = 0;
        foreach (var project in projects)
        {
            var dotnetArgs = new List<string> { "test", project, "--logger", "trx", "--results-directory", resultsDir };
            if (ci) dotnetArgs.AddRange(["--configuration", "Release"]);
            if (session is not null && project == ContractTests) dotnetArgs.AddRange(["--filter", $"FullyQualifiedName~.S{int.Parse(session):D2}."]);
            if (session is not null && project == OwnTests) continue;

            Console.WriteLine($"> dotnet {string.Join(' ', dotnetArgs)}");
            var result = Run(sq, Dotnet, dotnetArgs);
            if (result != 0) exitCode = result;
        }

        var results = Trx.ReadAll(resultsDir);
        if (results.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("No test results: the build failed. What it means:");
            foreach (var hint in Hints.ForBuild(paths, sq)) Console.WriteLine($"  • {hint}");
            if (ci) Report.WriteCi(paths, results, buildFailed: true);
            return exitCode == 0 ? 1 : exitCode;
        }

        Report.Print(paths, results);
        if (ci) Report.WriteCi(paths, results, buildFailed: false);
        return exitCode;
    }

    public static int ReportOnly(string sq, string[] options)
    {
        var paths = Paths.Read(sq);
        var results = Trx.ReadAll(Path.Combine(sq, ".sq", "results"));
        if (results.Count == 0)
        {
            Console.WriteLine("No results yet. Run: sq test");
            return 1;
        }
        Report.Print(paths, results);
        if (options.Contains("--ci")) Report.WriteCi(paths, results, buildFailed: false);
        return results.All(r => r.Passed || r.Skipped) ? 0 : 1;
    }

    /// <summary>Puts the managed files (Core/SharpQuest/) back into the Unity project when they're missing or stale.</summary>
    public static void RefreshManaged(Paths paths, bool quiet)
    {
        var unity = Path.Combine(paths.Sq, "unity");
        if (!Directory.Exists(unity) || !Directory.Exists(paths.Core)) return;
        foreach (var (from, to, managed) in UnityFiles(paths, unity))
        {
            if (!managed || (File.Exists(to) && File.ReadAllText(from) == File.ReadAllText(to))) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, overwrite: true);
            if (!quiet) Console.WriteLine($"Refreshed {paths.Show(to)}");
        }
    }

    // ---------------------------------------------------------------- sq update

    public static async Task<int> Update(string sq)
    {
        var paths = Paths.Read(sq);
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(sq, "sharpquest.json")))!;
        var upstream = (string?)config["upstream"] ?? "";
        var branch = (string?)config["branch"] ?? "main";
        var source = Environment.GetEnvironmentVariable("SQ_UPSTREAM_ZIP")
                     ?? $"https://codeload.github.com/{upstream}/zip/refs/heads/{branch}";

        if (upstream.Contains("CHANGE-ME") && Environment.GetEnvironmentVariable("SQ_UPSTREAM_ZIP") is null)
        {
            Console.WriteLine("SharpQuest/sharpquest.json still says CHANGE-ME. Ask your instructor for the course repository name.");
            return 1;
        }

        var before = Repo.Released(sq);
        var unityBefore = Repo.Hash(Path.Combine(sq, "unity"));
        var temp = Path.Combine(Path.GetTempPath(), "sq-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Console.WriteLine($"Downloading {source} ...");
            var zipPath = Path.Combine(temp, "upstream.zip");
            if (File.Exists(source)) File.Copy(source, zipPath);
            else
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                await using var download = await http.GetStreamAsync(source);
                await using var file = File.Create(zipPath);
                await download.CopyToAsync(file);
            }

            var extracted = Path.Combine(temp, "x");
            ZipFile.ExtractToDirectory(zipPath, extracted);
            // GitHub zips contain a single top-level folder, e.g. sharpquest-game-2026-main/
            var official = File.Exists(Path.Combine(extracted, "SharpQuest", "sq.cs"))
                ? extracted
                : Directory.GetDirectories(extracted).FirstOrDefault(d => File.Exists(Path.Combine(d, "SharpQuest", "sq.cs")))
                  ?? throw new InvalidDataException("the download doesn't look like the course's game-track repository");

            var changed = new List<string>();
            foreach (var path in Repo.Managed)
            {
                var from = Path.Combine(official, path);
                var to = Path.Combine(paths.Repo, path);
                if (!File.Exists(from) && !Directory.Exists(from)) continue;
                if (Repo.Hash(from) == Repo.Hash(to)) continue;
                Repo.Replace(from, to);
                changed.Add(path);
            }

            Console.WriteLine(changed.Count == 0 ? "Already up to date." : $"Updated: {string.Join(", ", changed)}");
            foreach (var added in Repo.Released(sq).Except(before))
                Console.WriteLine($"New session contract: S{added} — read SharpQuest/contracts/SharpQuest.Game.Contracts/S{added}/");
            if (Repo.Hash(Path.Combine(sq, "unity")) != unityBefore)
            {
                RefreshManaged(paths, quiet: false);
                Console.WriteLine("The files sq init puts in Unity changed: run sq init to see what's new (it never overwrites yours).");
            }
            if (changed.Count > 0) Console.WriteLine("Commit these changes along with your own work.");
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
        {
            Console.WriteLine($"Update failed: {ex.Message}");
            return 1;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The dotnet executable. SQ_DOTNET overrides it (used only to test sq itself).</summary>
    public static string Dotnet => Environment.GetEnvironmentVariable("SQ_DOTNET") ?? "dotnet";

    public static int Run(string workingDir, string file, IEnumerable<string> arguments, bool quiet = false) =>
        RunCapture(workingDir, file, arguments, quiet).ExitCode;

    public static (int ExitCode, string Output) RunCapture(string workingDir, string file, IEnumerable<string> arguments, bool quiet = true)
    {
        var psi = new ProcessStartInfo(file) { WorkingDirectory = workingDir, RedirectStandardOutput = quiet, RedirectStandardError = quiet };
        foreach (var a in arguments) psi.ArgumentList.Add(a);
        using var process = Process.Start(psi)!;
        if (!quiet) { process.WaitForExit(); return (process.ExitCode, ""); }
        var o = process.StandardOutput.ReadToEndAsync();
        var e = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, o.Result + e.Result);
    }

    internal static string CoreProject => CoreBuild;
}

/// <summary>Setup problems worth saying before a build: they explain a red row better than the row does.</summary>
static class Checks
{
    public static IEnumerable<string> Setup(Paths paths)
    {
        if (!paths.IsUnity) yield break;
        var asmdef = Path.Combine(paths.Core, "Game.Core.asmdef");
        if (!File.Exists(asmdef)) { yield return $"There's no {paths.Show(asmdef)}: run sq init."; yield break; }

        JsonNode? json = null;
        try { json = JsonNode.Parse(File.ReadAllText(asmdef), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }); }
        catch (JsonException) { }
        if (json is null) { yield return $"{paths.Show(asmdef)} isn't valid JSON. Unity will refuse it too."; yield break; }

        if ((bool?)json["noEngineReferences"] != true)
            yield return $"{paths.Show(asmdef)} lets Game.Core use UnityEngine. Tick \"No Engine References\" in its Inspector.";
        var refs = json["precompiledReferences"]?.AsArray().Select(n => (string?)n).ToList() ?? [];
        if ((bool?)json["overrideReferences"] == true && !refs.Contains("Newtonsoft.Json.dll"))
            yield return $"{paths.Show(asmdef)} doesn't list Newtonsoft.Json.dll under Assembly References, so Unity won't find the JSON library.";

        if (!Directory.Exists(paths.Core)) yield break;
        foreach (var file in Directory.EnumerateFiles(paths.Core, "*.cs", SearchOption.AllDirectories).Take(500))
        {
            var text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @":\s*(MonoBehaviour|ScriptableObject)\b"))
                yield return $"{paths.Show(file)} is a Unity script: it belongs in {paths.Show(paths.Unity)}/, not in Core.";
        }
    }

    /// <summary>The editor version the project was last opened with (ProjectSettings/ProjectVersion.txt).</summary>
    public static string? UnityVersion(Paths paths)
    {
        var file = Path.Combine(paths.Project, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(file)) return null;
        var m = Regex.Match(File.ReadAllText(file), @"m_EditorVersion:\s*(\S+)");
        return m.Success ? m.Groups[1].Value : null;
    }
}

/// <summary>
/// A failed build, explained. The compiler's messages are correct but speak .NET; these say what it means
/// for a game whose rules have to compile in Unity too.
/// </summary>
static class Hints
{
    static readonly string[] SystemNames =
    [
        "Math", "List", "Dictionary", "HashSet", "IEnumerable", "IReadOnlyList", "Func", "Action", "Enumerable",
        "ArgumentException", "ArgumentNullException", "InvalidOperationException", "Random", "TimeSpan", "Exception", "Console", "Task",
    ];

    public static IEnumerable<string> ForBuild(Paths paths, string sq)
    {
        var (_, output) = Commands.RunCapture(sq, Commands.Dotnet, ["build", Commands.CoreProject, "-nologo", "-v", "q"]);
        var errors = output.Split('\n').Select(l => l.Trim()).Where(l => l.Contains(": error ")).Distinct().ToList();
        if (errors.Count == 0)
        {
            yield return "Your game's rules (Assets/Game/Core) build. The error is in your wiring or your tests: it's the first error above.";
            yield break;
        }

        var said = new HashSet<string>();
        foreach (var line in errors.Take(40))
        {
            var where = Where(paths, line);
            string? hint =
                Regex.IsMatch(line, @"'Unity(Engine|Editor)'|UnityEngine|UnityEditor")
                    ? $"{where} uses Unity. Game.Core can't: move that script to {paths.Show(paths.Unity)}/, or pass in what it needs (Time.deltaTime, a position) as a parameter."
                : Regex.IsMatch(line, @"not available in C# 9|language version 1\d|CS8773|CS8936|CS9058")
                    ? $"{where}: Unity compiles C# 9, and that's newer C#. Common ones: 'namespace X;' → 'namespace X {{ … }}', '[1, 2]' → 'new[] {{ 1, 2 }}', a class with (parameters) after its name → a constructor."
                : Regex.Match(line, @"error CS0234: The type or namespace name '(\w+)' does not exist in the namespace 'System") is { Success: true } m1
                    ? $"{where}: System.{m1.Groups[1].Value} isn't in .NET Standard 2.1, which is what Unity compiles against. (DateOnly and TimeOnly are the usual ones: use DateTime.)"
                : Regex.Match(line, @"error CS(0246|0103): The (?:type or namespace )?name '(\w+)'") is { Success: true } m2 && SystemNames.Contains(m2.Groups[2].Value)
                    ? $"{where}: Unity has no implicit usings. Add the 'using' line yourself: using System; using System.Collections.Generic; using System.Linq;"
                : Regex.IsMatch(line, @"error CS86\d\d")
                    ? $"{where}: a nullable warning, which counts as an error (session 2: zero nullable warnings)."
                : null;
            if (hint is not null && said.Add(hint)) yield return hint;
        }
        if (said.Count == 0) yield return $"Your game's rules don't build. First error: {Short(paths, errors[0])}";
    }

    static string Where(Paths paths, string line)
    {
        var m = Regex.Match(line, @"^(.+?\.cs)\((\d+),\d+\)");
        return m.Success ? $"{paths.Show(m.Groups[1].Value)}, line {m.Groups[2].Value}" : "Your code";
    }

    static string Short(Paths paths, string line) =>
        Regex.Replace(line, @"\s*\[[^\]]+\.csproj\]$", "").Replace(paths.Repo + Path.DirectorySeparatorChar, "");
}

record TestResult(string Group, string Name, bool Passed, bool Skipped, string? Message);

static class Trx
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static List<TestResult> ReadAll(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "*.trx", SearchOption.AllDirectories).SelectMany(Read).ToList();
    }

    private static IEnumerable<TestResult> Read(string path)
    {
        var doc = XDocument.Load(path);
        var classes = doc.Descendants(Ns + "UnitTest").ToDictionary(
            t => (string)t.Attribute("id")!,
            t => (string?)t.Element(Ns + "TestMethod")?.Attribute("className") ?? "");

        foreach (var r in doc.Descendants(Ns + "UnitTestResult"))
        {
            var className = classes.GetValueOrDefault((string)r.Attribute("testId")!, "");
            var outcome = (string?)r.Attribute("outcome") ?? "";
            var message = r.Element(Ns + "Output")?.Element(Ns + "ErrorInfo")?.Element(Ns + "Message")?.Value;
            yield return new TestResult(Group(className), ShortName((string?)r.Attribute("testName") ?? "?"),
                outcome == "Passed", outcome == "NotExecuted", message);
        }
    }

    /// <summary>"SharpQuest.Game.Tests.S02.ContentTests" → "02"; anything else (your NUnit tests) → "own".</summary>
    private static string Group(string className)
    {
        var m = Regex.Match(className, @"^SharpQuest\.Game\.Tests\.S(\d{2})\.");
        return m.Success ? m.Groups[1].Value : "own";
    }

    private static string ShortName(string testName)
    {
        var paren = testName.IndexOf('(');
        var head = paren < 0 ? testName : testName[..paren];
        var parts = head.Split('.');
        var shortHead = parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : head;
        return paren < 0 ? shortHead : shortHead + testName[paren..];
    }
}

static class Report
{
    /// <summary>What each session's row is about, as students know it.</summary>
    static readonly Dictionary<string, string> Titles = new()
    {
        ["02"] = "content", ["03"] = "tests", ["04"] = "kinds", ["05"] = "collections", ["06"] = "events",
        ["07"] = "queries", ["08"] = "save game", ["09"] = "view model", ["10"] = "API", ["11"] = "database", ["12"] = "attributes",
    };

    static string Label(string group) => group == "own" ? "Your tests" : $"S{group} {Titles.GetValueOrDefault(group, "")}".TrimEnd();

    public static void Print(Paths paths, List<TestResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("──────────── SharpQuest · game ────────────");
        var notStarted = new List<string>();
        foreach (var group in Ordered(results))
        {
            if (NotStarted(group)) { notStarted.Add(group.Key); continue; }
            var (passed, total) = Count(group);
            var mark = passed == total ? "OK  " : "FAIL";
            var extra = group.Key == "own" ? "" : $"   contract ≈ {ContractPoints(passed, total):0.0} / 4";
            Console.WriteLine($" {mark} {Label(group.Key),-15} {passed,3}/{total,-3}{extra}");
            foreach (var (name, message) in Failures(group))
                Console.WriteLine($"        ✗ {name}\n          {message}");
        }
        if (notStarted.Count > 0)
        {
            Console.WriteLine($" --   Not started: {string.Join(", ", notStarted.Select(Label))}");
            Console.WriteLine("      (a session starts when SharpQuest/Wiring/ has a class for it)");
        }
        Console.WriteLine("───────────────────────────────────────────");
        Console.WriteLine("Contract points are indicative. Your score comes from the CI run on GitHub.");
    }

    public static void WriteCi(Paths paths, List<TestResult> results, bool buildFailed)
    {
        var sessions = new JsonObject();
        foreach (var group in Ordered(results).Where(g => g.Key != "own"))
        {
            var (passed, total) = Count(group);
            sessions[group.Key] = NotStarted(group)
                ? new JsonObject { ["started"] = false }
                : new JsonObject { ["started"] = true, ["passed"] = passed, ["total"] = total };
        }
        var (ownPassed, ownTotal) = Count(results.Where(r => r.Group == "own"));

        var score = new JsonObject
        {
            ["schema"] = 2,
            ["track"] = "game",
            ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["commit"] = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            ["buildFailed"] = buildFailed,
            ["contractsHash"] = Repo.Hash(Path.Combine(paths.Sq, "contracts")),
            ["labs"] = sessions,   // same key as the labs' score.json, so the collector reads both
            ["ownTests"] = new JsonObject { ["passed"] = ownPassed, ["total"] = ownTotal },
            ["nullableEscapes"] = new JsonArray(Repo.NullableEscapes(paths).Select(f => (JsonNode)f).ToArray()),
            ["unityVersion"] = Checks.UnityVersion(paths),
        };
        File.WriteAllText(Path.Combine(paths.Sq, "score.json"),
            score.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        var summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrEmpty(summaryPath)) return;

        var md = new StringBuilder();
        md.AppendLine("## SharpQuest checks · game track").AppendLine();
        if (buildFailed)
            md.AppendLine("**The build failed.** Open the *Build and test* step above: the lines after \"What it means\" say what to fix.");
        else
        {
            md.AppendLine("| | Passed | Contract (indicative) |").AppendLine("|---|---|---|");
            foreach (var group in Ordered(results))
            {
                if (NotStarted(group)) { md.AppendLine($"| ⏳ {Label(group.Key)} | not started | — |"); continue; }
                var (p, t) = Count(group);
                md.AppendLine($"| {(p == t ? "✅" : "❌")} {Label(group.Key)} | {p} / {t} | {(group.Key == "own" ? "—" : $"{ContractPoints(p, t):0.0} / 4")} |");
            }
            var failures = results.Where(r => !r.Passed && !r.Skipped).ToList();
            if (failures.Count > 0)
            {
                md.AppendLine().AppendLine("### Failing").AppendLine();
                foreach (var (name, message) in Failures(failures)) md.AppendLine($"- `{name}` — {message}");
            }
        }
        File.AppendAllText(summaryPath, md.ToString());
    }

    private static IEnumerable<IGrouping<string, TestResult>> Ordered(IEnumerable<TestResult> results) =>
        results.GroupBy(r => r.Group).OrderBy(g => g.Key == "own" ? "zz" : g.Key);

    /// <summary>Contract tests skip only when the session's wiring class is missing: all skipped = not started.</summary>
    private static bool NotStarted(IGrouping<string, TestResult> group) => group.Key != "own" && group.All(r => r.Skipped);

    private static IEnumerable<(string Name, string Message)> Failures(IEnumerable<TestResult> results)
    {
        foreach (var same in results.Where(r => !r.Passed && !r.Skipped).GroupBy(r => FirstLine(r.Message)))
        {
            if (same.Count() >= 3) yield return ($"{same.Count()} tests", same.Key);
            else foreach (var r in same) yield return (r.Name, same.Key);
        }
    }

    private static (int Passed, int Total) Count(IEnumerable<TestResult> results)
    {
        var counted = results.Where(r => !r.Skipped).ToList();
        return (counted.Count(r => r.Passed), counted.Count);
    }

    private static double ContractPoints(int passed, int total) => total == 0 ? 0 : Math.Round(4.0 * passed / total, 1);

    private static string FirstLine(string? text) => (text ?? "").Split('\n', 2)[0].Trim();
}

static class Repo
{
    /// <summary>Everything `sq update` overwrites with the official copy, relative to the top of the repository.</summary>
    public static readonly string[] Managed =
    [
        "SharpQuest/contracts",
        "SharpQuest/build",
        "SharpQuest/unity",
        "SharpQuest/Directory.Build.props",
        "SharpQuest/Directory.Build.targets",
        "SharpQuest/Directory.Packages.props",
        "SharpQuest/global.json",
        "SharpQuest/SharpQuest.Game.slnx",
        "SharpQuest/sq.cs",
        "SharpQuest/sq.cmd",
        "SharpQuest/sq.sh",
        "SharpQuest/tools/sq/sq.csproj",
        ".github/workflows/ci.yml",
        "sq.cmd",
        "sq.sh",
    ];

    /// <summary>The SharpQuest/ folder: found from anywhere inside the repository, or from its top.</summary>
    public static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SharpQuest.Game.slnx"))) return dir.FullName;
            var below = Path.Combine(dir.FullName, "SharpQuest");
            if (File.Exists(Path.Combine(below, "SharpQuest.Game.slnx"))) return below;
        }
        Console.WriteLine("Run sq from inside your repository (the folder with SharpQuest/ in it).");
        Environment.Exit(2);
        return "";
    }

    public static List<string> Released(string sq)
    {
        var file = Path.Combine(sq, "contracts", "RELEASED");
        if (!File.Exists(file)) return [];
        return File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
    }

    /// <summary>Files in Game.Core that switch nullable checking off. Reported to the instructor, not blocked.</summary>
    public static IEnumerable<string> NullableEscapes(Paths paths)
    {
        if (!Directory.Exists(paths.Core)) yield break;
        var pattern = new Regex(@"^\s*#\s*(nullable\s+disable|pragma\s+warning\s+disable\s+.*CS86)", RegexOptions.Multiline);
        foreach (var file in Directory.EnumerateFiles(paths.Core, "*.cs", SearchOption.AllDirectories))
            if (pattern.IsMatch(File.ReadAllText(file))) yield return paths.Show(file);
    }

    /// <summary>Content hash of a file or a folder (paths + contents, line endings normalised). Empty if missing.</summary>
    public static string Hash(string path)
    {
        IEnumerable<string> files =
            File.Exists(path) ? [path] :
            Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
                .Order(StringComparer.Ordinal) :
            [];

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var any = false;
        foreach (var f in files)
        {
            any = true;
            var relative = File.Exists(path) ? "" : Path.GetRelativePath(path, f).Replace('\\', '/');
            sha.AppendData(Encoding.UTF8.GetBytes(relative + "\n"));
            sha.AppendData(Encoding.UTF8.GetBytes(File.ReadAllText(f).Replace("\r\n", "\n")));
        }
        return any ? Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant()[..16] : "";
    }

    public static void Replace(string from, string to)
    {
        if (File.Exists(from))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, overwrite: true);
            return;
        }
        if (Directory.Exists(to))
            foreach (var file in Directory.EnumerateFiles(to, "*", SearchOption.AllDirectories)
                         .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj")))
                File.Delete(file);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
