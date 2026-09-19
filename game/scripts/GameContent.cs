using Godot;
using Kiln.Data.Loading;
using Kiln.Data.Validation;

namespace Kiln.Game;

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

    /// <summary>
    /// Makes content available inside the Godot editor, where no autoload runs.
    /// </summary>
    /// <remarks>
    /// Without this a <c>[Tool]</c> script has nothing to read, which is why the greybox kit
    /// shipped with no editor preview: the pieces existed only once the game was running, and
    /// laying out a village means placing a couple of hundred of them blind.
    /// <para>
    /// The validator is deliberately not run here. In the editor the answer to broken content
    /// is the error list in CI or a run of the game, not a wall of output every time a scene
    /// is opened — and a piece whose preview is missing is its own report.
    /// </para>
    /// </remarks>
    public static bool EnsureLoadedForEditor()
    {
        if (_database is not null) return true;
        if (!Engine.IsEditorHint() || _editorLoadFailed) return false;

        var result = ContentLoader.Load(new GodotContentFileSource());

        if (!result.Success)
        {
            // Latched: two hundred pieces asking the same broken question would otherwise
            // produce two hundred copies of the same answer.
            _editorLoadFailed = true;
            GD.PushWarning(
                $"[content] editor previews unavailable — {result.Errors.Count} load error(s). "
                + "Run the game or the validator for the details.");

            return false;
        }

        _database = result.Database;
        return true;
    }

    private static bool _editorLoadFailed;

    /// <summary>Reloads from disk. Editor-only convenience for iterating on data without restarting.</summary>
    public static bool Reload()
    {
        _database = null;
        _editorLoadFailed = false;
        Visual.VisualRegistry.ClearCache();
        World.KitPiece.ClearCaches();
        return Load();
    }
}
