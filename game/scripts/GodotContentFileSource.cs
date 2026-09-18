using System.Collections.Generic;
using Godot;
using Shardfall.Data.Loading;

// Godot.FileAccess and System.IO.FileAccess collide under implicit usings.
using FileAccess = Godot.FileAccess;

namespace Shardfall.Game;

/// <summary>
/// Reads content JSON through Godot's FileAccess so it works identically in the editor and
/// in an exported build, where <c>res://</c> lives inside a .pck that System.IO cannot see.
/// </summary>
public sealed class GodotContentFileSource(string root = "res://data") : IContentFileSource
{
    private readonly string _root = root.TrimEnd('/');

    private string Absolute(string relative) => $"{_root}/{relative}";

    public bool DirectoryExists(string relativeDir) => DirAccess.DirExistsAbsolute(Absolute(relativeDir));

    public bool FileExists(string relativePath) => FileAccess.FileExists(Absolute(relativePath));

    public IEnumerable<string> EnumerateJsonFiles(string relativeDir)
    {
        var results = new List<string>();
        Collect(relativeDir, results);
        results.Sort(System.StringComparer.Ordinal);
        return results;
    }

    private void Collect(string relativeDir, List<string> results)
    {
        using var dir = DirAccess.Open(Absolute(relativeDir));
        if (dir is null) return;

        dir.ListDirBegin();
        for (var name = dir.GetNext(); !string.IsNullOrEmpty(name); name = dir.GetNext())
        {
            if (name is "." or "..") continue;

            var relative = $"{relativeDir}/{name}";

            if (dir.CurrentIsDir())
            {
                Collect(relative, results);
            }
            else if (name.EndsWith(".json", System.StringComparison.Ordinal))
            {
                results.Add(relative);
            }
            // An exported build may present "file.json.remap" instead of the file itself.
            else if (name.EndsWith(".json.remap", System.StringComparison.Ordinal))
            {
                results.Add(relative[..^".remap".Length]);
            }
        }

        dir.ListDirEnd();
    }

    public string ReadAllText(string relativePath)
    {
        using var file = FileAccess.Open(Absolute(relativePath), FileAccess.ModeFlags.Read);
        if (file is null)
        {
            throw new System.IO.IOException(
                $"Could not open {Absolute(relativePath)}: {FileAccess.GetOpenError()}");
        }

        return file.GetAsText();
    }
}
