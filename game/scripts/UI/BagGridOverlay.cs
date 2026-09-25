using Godot;

namespace Kiln.Game.UI;

/// <summary>Drawn over the bag's items: where a carried item would land (REF-10).</summary>
public partial class BagGridOverlay : Control
{
    public BagGrid Grid { get; set; } = null!;
    public Rect2I? Cells { get; set; }
    public bool Fits { get; set; }

    public override void _Ready() => Size = Grid.CustomMinimumSize;

    public override void _Draw()
    {
        if (Cells is not { } cells) return;

        // Clipped to the page: a shadow hanging off the edge says "not here" well enough.
        var x0 = Mathf.Max(0, cells.Position.X);
        var y0 = Mathf.Max(0, cells.Position.Y);
        var x1 = Mathf.Min(Grid.Columns, cells.End.X);
        var y1 = Mathf.Min(Grid.Rows, cells.End.Y);

        if (x1 <= x0 || y1 <= y0) return;

        DrawRect(Grid.RectOf(x0, y0, x1 - x0, y1 - y0), Fits ? BagGrid.Fits : BagGrid.Blocked);
    }
}
