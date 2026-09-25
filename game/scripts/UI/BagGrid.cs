using System;
using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// The squares of one page of the bag (REF-10), and where a carried item would land.
/// </summary>
/// <remarks>
/// The items themselves are <see cref="ItemView"/> children laid over the squares. A drop on
/// an empty square lands here, a drop on an item lands on the item; both are handed to the
/// panel as a screen point, so the panel works out the square the same way for either.
/// </remarks>
public partial class BagGrid : Control
{
    private static readonly Color Cell = new(0.12f, 0.13f, 0.16f, 0.92f);
    private static readonly Color Edge = new(0.22f, 0.25f, 0.30f);
    internal static readonly Color Fits = new(0.45f, 0.85f, 0.5f, 0.35f);
    internal static readonly Color Blocked = new(0.95f, 0.35f, 0.3f, 0.35f);

    public int Columns { get; init; }
    public int Rows { get; init; }
    public int CellSize { get; init; }
    public int Gap { get; init; }

    public int Step => CellSize + Gap;

    /// <summary>Asked whether a payload may be let go at a screen point.</summary>
    public Func<Vector2, Variant, bool>? CanTake { get; set; }

    /// <summary>A payload let go at a screen point.</summary>
    public Action<Vector2, Variant>? Take { get; set; }

    private BagGridOverlay _overlay = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2((Columns * Step) - Gap, (Rows * Step) - Gap);

        // Drawn over the items, so the shadow of where a carried item would land is seen
        // over whatever it would land on.
        _overlay = new BagGridOverlay { Grid = this, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_overlay);
    }

    /// <summary>The square under a screen point, which may lie outside the page.</summary>
    public Vector2I CellAt(Vector2 screen)
    {
        var local = GetGlobalTransform().AffineInverse() * screen;

        return new Vector2I(Mathf.FloorToInt(local.X / Step), Mathf.FloorToInt(local.Y / Step));
    }

    /// <summary>Where an item standing on a square is drawn.</summary>
    public Rect2 RectOf(int x, int y, int w, int h) =>
        new(new Vector2(x * Step, y * Step), new Vector2((w * Step) - Gap, (h * Step) - Gap));

    /// <summary>Shows where a carried item would land, green when it fits; null clears it.</summary>
    public void Preview(Rect2I? cells, bool fits)
    {
        _overlay.Cells = cells;
        _overlay.Fits = fits;
        _overlay.QueueRedraw();
    }

    /// <summary>Keeps the overlay last, above items added after it.</summary>
    public void Raise() => MoveChild(_overlay, GetChildCount() - 1);

    public override void _Draw()
    {
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                var rect = RectOf(x, y, 1, 1);

                DrawRect(rect, Cell);
                DrawRect(rect, Edge, filled: false, width: 1);
            }
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        CanTake?.Invoke(GetGlobalTransform() * atPosition, data) ?? false;

    public override void _DropData(Vector2 atPosition, Variant data) =>
        Take?.Invoke(GetGlobalTransform() * atPosition, data);
}
