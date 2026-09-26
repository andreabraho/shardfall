using Godot;
using FileAccess = Godot.FileAccess;

namespace Kiln.Game.Saving;

/// <summary>
/// Brings the saves and settings across from the folder the game used while it was called
/// Kiln (2026-09-26, the title became Temins). Godot names the user folder after the game, so
/// the new title starts with an empty one; without this every character made so far would
/// look lost.
/// </summary>
/// <remarks>
/// Copies, never moves: the old folder is left as it was, so nothing can be lost if the copy
/// is interrupted. Runs only while the new folder has no saves and no settings of its own, so
/// once the player has played under the new name it never touches anything again.
/// </remarks>
public static class UserDataMove
{
    /// <summary>The folder name the game used before it was renamed.</summary>
    private const string OldName = "Kiln";

    public static void Run()
    {
        var current = OS.GetUserDataDir().Replace('\\', '/');
        var parent = current.GetBaseDir();
        var old = parent.PathJoin(OldName);

        if (old == current || !DirAccess.DirExistsAbsolute(old)) return;

        var hasSaves = DirAccess.DirExistsAbsolute(current.PathJoin("saves"));
        var hasSettings = FileAccess.FileExists(current.PathJoin("settings.cfg"));

        if (hasSaves || hasSettings) return;

        var copied = 0;

        if (DirAccess.DirExistsAbsolute(old.PathJoin("saves"))) copied += Copy(old.PathJoin("saves"), current.PathJoin("saves"));

        if (FileAccess.FileExists(old.PathJoin("settings.cfg"))
            && DirAccess.CopyAbsolute(old.PathJoin("settings.cfg"), current.PathJoin("settings.cfg")) == Error.Ok)
        {
            copied++;
        }

        if (copied > 0) GD.Print($"[saves] brought {copied} file(s) across from {old}");
    }

    private static int Copy(string from, string to)
    {
        DirAccess.MakeDirRecursiveAbsolute(to);

        using var dir = DirAccess.Open(from);

        if (dir is null) return 0;

        var count = 0;

        foreach (var file in dir.GetFiles())
        {
            if (DirAccess.CopyAbsolute(from.PathJoin(file), to.PathJoin(file)) == Error.Ok) count++;
        }

        foreach (var sub in dir.GetDirectories()) count += Copy(from.PathJoin(sub), to.PathJoin(sub));

        return count;
    }
}
