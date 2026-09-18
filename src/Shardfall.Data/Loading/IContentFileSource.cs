namespace Shardfall.Data.Loading;

/// <summary>
/// Where content JSON is read from. Exists because an exported Godot build packs
/// <c>res://</c> into a .pck that <see cref="System.IO"/> cannot see — the game reads
/// through Godot's FileAccess, while tests, the validator and the balance simulator read
/// straight from disk. Same loader, same rules, both paths.
/// </summary>
/// <remarks>All paths are relative to the data root and use '/' separators.</remarks>
public interface IContentFileSource
{
    bool DirectoryExists(string relativeDir);

    /// <summary>Relative paths of every .json file under <paramref name="relativeDir"/>, recursively.</summary>
    IEnumerable<string> EnumerateJsonFiles(string relativeDir);

    bool FileExists(string relativePath);

    string ReadAllText(string relativePath);
}

/// <summary>Reads content from an ordinary folder. Used by tests, tools and the editor.</summary>
public sealed class DiskContentFileSource(string root) : IContentFileSource
{
    public string Root { get; } = root;

    private string Absolute(string relative) =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public bool DirectoryExists(string relativeDir) => Directory.Exists(Absolute(relativeDir));

    public IEnumerable<string> EnumerateJsonFiles(string relativeDir)
    {
        var dir = Absolute(relativeDir);
        if (!Directory.Exists(dir)) yield break;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            yield return Path.GetRelativePath(Root, file).Replace('\\', '/');
        }
    }

    public bool FileExists(string relativePath) => File.Exists(Absolute(relativePath));

    public string ReadAllText(string relativePath) => File.ReadAllText(Absolute(relativePath));
}
