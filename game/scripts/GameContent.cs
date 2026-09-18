using Godot;
using Sohan.Data.Loading;
using Sohan.Data.Validation;

namespace Sohan.Game;

/// <summary>
/// Loads the content database once at startup and holds it for the session.
/// <para>
/// In the editor and in debug builds it also runs the full validator, so a designer editing
/// JSON sees the same errors CI would report without waiting for a push. Release builds skip
/// validation — the content shipped in the build already passed it.
/// </para>
/// </summary>
public static class GameContent
{
    private static ContentDatabase? _database;

    public static bool IsLoaded => _database is not null;

    public static ContentDatabase Database =>
        _database ?? throw new System.InvalidOperationException(
            "GameContent.Load() must run before content is accessed. It is called from Bootstrap._Ready().");

    /// <summary>Returns false when content failed to load or validate, so the caller can abort startup loudly.</summary>
    public static bool Load()
    {
        var sw = Time.GetTicksMsec();
        var result = ContentLoader.Load(new GodotContentFileSource());

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                GD.PushError($"[content] {error}");
            }

            GD.PushError($"[content] {result.Errors.Count} load error(s). The game cannot start.");
            return false;
        }

        _database = result.Database;

        if (OS.IsDebugBuild())
        {
            var report = ContentValidator.Validate(_database);
            if (report.Findings.Count > 0)
            {
                GD.Print($"[content] validation findings:\n{report.Format()}");
            }

            if (report.HasErrors)
            {
                GD.PushError($"[content] {report.ErrorCount} validation error(s). Fix these before committing.");
                return false;
            }
        }

        GD.Print($"[content] loaded {_database.TotalDefinitions} definitions in {Time.GetTicksMsec() - sw} ms");
        return true;
    }

    /// <summary>Reloads from disk. Editor-only convenience for iterating on data without restarting.</summary>
    public static bool Reload()
    {
        _database = null;
        Visual.VisualRegistry.ClearCache();
        return Load();
    }
}
