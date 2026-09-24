using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// Where the bars across the top of the screen end, so each thing placed there goes below
/// the last rather than over it.
/// </summary>
/// <remarks>
/// A shard and a boss can be fought at once — the Spider Queen's lair has a stone in reach —
/// and their bars were each pinned to their own height, one over the other. The shard's bar
/// keeps its place; the boss's goes under it; a notice goes under both.
/// </remarks>
public static class TopBars
{
    /// <summary>Where a bar sits when nothing is above it.</summary>
    public const float Top = 92f;

    private const float Gap = 8f;

    /// <summary>The screen y just below <paramref name="bar"/>, or <see cref="Top"/> while it is hidden.</summary>
    public static float Below(Control? bar) =>
        bar is { Visible: true } ? bar.GetGlobalRect().End.Y + Gap : Top;

    /// <summary>The first free line under every bar showing.</summary>
    public static float Free(SceneTree tree)
    {
        var y = Top;

        foreach (var node in tree.GetNodesInGroup("top_bars"))
        {
            if (node is ITopBar { Panel: { Visible: true } panel }) y = Mathf.Max(y, panel.GetGlobalRect().End.Y + Gap);
        }

        return y;
    }
}

/// <summary>A bar across the top of the screen.</summary>
public interface ITopBar
{
    Control Panel { get; }
}
