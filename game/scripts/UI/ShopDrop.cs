using System;
using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// The merchant's goods as a place to let go of something carried from the bag: whatever is
/// dropped anywhere over them is sold (REF-12).
/// </summary>
/// <remarks>
/// This takes a drop between the icons; the icons take one on themselves the same way.
/// </remarks>
public partial class ShopDrop : MarginContainer
{
    public Func<Variant, bool>? CanTake { get; set; }

    public Action<Variant>? Take { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => CanTake?.Invoke(data) ?? false;

    public override void _DropData(Vector2 atPosition, Variant data) => Take?.Invoke(data);
}
