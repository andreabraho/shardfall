using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// Whether a full-screen panel currently owns the player's input.
/// </summary>
/// <remarks>
/// Click-to-move reads the mouse button directly in <c>_Process</c> rather than through
/// <c>_UnhandledInput</c>, because a held button has to keep issuing move orders and an event
/// only fires on press. That is the right behaviour for movement and the wrong behaviour for
/// everything else: polling bypasses the UI entirely, so clicking a button in the inventory
/// also walked the character across the arena.
/// <para>
/// A panel raises this while it is open, and the systems that poll ask before acting.
/// Keyboard actions are blocked by the same flag: a focused panel should not also be firing
/// skills.
/// </para>
/// </remarks>
public static class UiState
{
    private static int _open;

    /// <summary>True while any panel is open, so gameplay input should stand down.</summary>
    public static bool ModalOpen => _open > 0;

    public static void Push() => _open++;

    public static void Pop() => _open = Mathf.Max(0, _open - 1);

    /// <summary>Called by a panel whenever its visibility changes, so the count cannot drift.</summary>
    public static void SetOpen(ref bool tracked, bool open)
    {
        if (tracked == open) return;

        tracked = open;

        Audio.AudioDirector.Play(open ? Kiln.Data.Ids.Sounds.SndUiOpen : Kiln.Data.Ids.Sounds.SndUiClose);

        if (open) Push();
        else Pop();
    }

    /// <summary>Reset hook for scene changes, so a panel freed while open cannot strand the count.</summary>
    public static void Reset()
    {
        _open = 0;
        _side.Clear();
    }

    // -- Side panels (REF-07) ----------------------------------------------

    private static readonly System.Collections.Generic.HashSet<Control> _side = [];

    /// <summary>
    /// True while a side panel is open. They leave play running — the player moves and fights
    /// with the map or the bag up — so this only settles who the Escape key closes first.
    /// </summary>
    public static bool SideOpen
    {
        get
        {
            _side.RemoveWhere(panel => !GodotObject.IsInstanceValid(panel));
            return _side.Count > 0;
        }
    }

    /// <summary>
    /// Opens or closes a side panel: one kept at the edge of the screen and open during play,
    /// the map and the inventory (2026-09-24). Its area belongs to it — a click there is not a
    /// move — and everything else on screen is still the world.
    /// </summary>
    public static void SetSide(ref bool tracked, Control panel, bool open)
    {
        if (tracked == open) return;

        tracked = open;
        Audio.AudioDirector.Play(open ? Kiln.Data.Ids.Sounds.SndUiOpen : Kiln.Data.Ids.Sounds.SndUiClose);

        if (open) _side.Add(panel);
        else _side.Remove(panel);
    }

    /// <summary>Whether a screen point is on an open side panel.</summary>
    public static bool PointerOverPanel(Vector2 screen)
    {
        foreach (var panel in _side)
        {
            if (GodotObject.IsInstanceValid(panel) && panel.IsVisibleInTree() && panel.GetGlobalRect().HasPoint(screen)) return true;
        }

        return false;
    }
}
