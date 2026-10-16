using System.Reflection;

namespace SharpQuest.Game.Contracts;

/// <summary>
/// Finds your game's files from the wiring, so the course's tests read the very same content your game loads in Unity.
/// The folders come from SharpQuest/game.props.
/// </summary>
public static class GameFiles
{
    private static string Setting(string name) =>
        typeof(GameFiles).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == name)?.Value
        ?? throw new InvalidOperationException($"The contracts were built without {name}. Run sq test, not a bare dotnet build.");

    /// <summary>Your Unity project: the folder with Assets/ and ProjectSettings/.</summary>
    public static string ProjectDirectory => Setting("GameProjectDir");

    /// <summary>Your rules: Assets/Game/Core/, unless game.props says otherwise.</summary>
    public static string CoreDirectory => Setting("GameCoreDir");

    /// <summary>Your content: Assets/Game/Content/, unless game.props says otherwise.</summary>
    public static string ContentDirectory => Setting("GameContentDir");

    /// <summary>The engine game.props names (Unity unless told otherwise).</summary>
    public static string Engine => Setting("GameEngine");

    /// <summary>The full path of a file in your content folder, e.g. <c>ContentPath("enemies.json")</c>.</summary>
    public static string ContentPath(string file) =>
        Path.GetFullPath(Path.Combine(ContentDirectory, file));

    /// <summary>The text of a file in your content folder. The same text Unity's TextAsset gives your game.</summary>
    public static string ReadContent(string file)
    {
        var path = ContentPath(file);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"There is no {file} in your content folder ({ContentDirectory}). Content lives in your Unity project, " +
                "so the game and the tests read the same file.", path);
        return File.ReadAllText(path);
    }
}
