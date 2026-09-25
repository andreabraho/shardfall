using System;
using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// A tab that turns the bag to one of its pages (REF-10). Held over while carrying an item,
/// it turns the page too, so an item can be carried from one page to the other.
/// </summary>
public partial class BagPageTab : Button
{
    public int Page { get; init; }

    /// <summary>Told when a carried item is held over the tab.</summary>
    public Action<int>? HeldOver { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        HeldOver?.Invoke(Page);
        return false;
    }
}
