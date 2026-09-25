using System;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// One item as the bag and the worn-gear figure draw it (REF-10): its icon on a square in its
/// rarity's colour, the count of a stack in the corner, a hard border when it is locked. An
/// empty worn slot is the same square with the slot's name in it.
/// </summary>
/// <remarks>
/// Drawn by hand rather than built from a Button, so it can be dragged: a Button takes the
/// press for itself, and the gesture the bag is organised by — pick up, carry, let go — never
/// starts. What a drag or a drop means is decided by the panel that owns the view; the view
/// only reports it.
/// </remarks>
public partial class ItemView : Control
{
    private static readonly Color Empty = new(0.12f, 0.13f, 0.16f, 0.92f);
    private static readonly Color Edge = new(0.25f, 0.28f, 0.34f);

    /// <summary>The item shown, or null for an empty worn slot.</summary>
    public ItemInstance? Item { get; private set; }

    /// <summary>The worn slot this view stands for, or null for an item in the bag.</summary>
    public EquipSlot? Worn { get; init; }

    /// <summary>Written faintly in an empty slot: which piece goes there.</summary>
    public string Placeholder { get; init; } = "";

    /// <summary>Asked when a drag starts here, with the local point; returns the payload.</summary>
    public Func<ItemView, Vector2, Variant>? DragFrom { get; set; }

    /// <summary>Asked whether a payload may be let go at a screen point over this view.</summary>
    public Func<Vector2, Variant, bool>? CanTake { get; set; }

    /// <summary>A payload let go at a screen point over this view.</summary>
    public Action<Vector2, Variant>? Take { get; set; }

    private Texture2D? _icon;
    private Color _colour = Colors.White;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    /// <summary>Shows an item, or an empty slot.</summary>
    public void Display(ItemInstance? item)
    {
        Item = item;

        var spec = item is null ? null : GameItems.Spec(item.DefId);

        _colour = World.LootDrop.RarityColour(spec?.Rarity ?? Rarity.Common);
        _icon = spec is null ? null : ItemIcons.For(item!.DefId, spec);

        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var font = ThemeDB.FallbackFont;

        if (Item is null)
        {
            DrawRect(rect, Empty);
            DrawRect(rect, Edge, filled: false, width: 1);

            if (Placeholder.Length > 0)
            {
                DrawMultilineString(font, new Vector2(3, (Size.Y / 2) - 2), Placeholder, HorizontalAlignment.Center,
                    Size.X - 6, 10, 2, new Color(0.5f, 0.54f, 0.6f, 0.7f));
            }

            return;
        }

        DrawRect(rect, _colour * new Color(1, 1, 1, 0.16f));

        if (_icon is not null)
        {
            // Square, centred, as large as the shorter side allows with a little room.
            var side = Mathf.Min(Size.X, Size.Y) - 8;
            var at = new Vector2((Size.X - side) / 2, (Size.Y - side) / 2);

            DrawTextureRect(_icon, new Rect2(at, new Vector2(side, side)), false, _colour.Lerp(Colors.White, 0.25f));
        }
        else
        {
            // No icon for this item: its name, as small as it has to be.
            DrawMultilineString(font, new Vector2(3, 13), GameItems.NameOf(Item), HorizontalAlignment.Center,
                Size.X - 6, 10, 4, _colour);
        }

        if (Item.Count > 1)
        {
            var count = Item.Count.ToString(L10n.Culture);
            var width = font.GetStringSize(count, HorizontalAlignment.Left, -1, 12).X;

            DrawString(font, new Vector2(Size.X - width - 4, Size.Y - 4), count, HorizontalAlignment.Left, -1, 12, Colors.White);
        }

        // Locked: outlined, not dimmed — protected, not unavailable.
        if (Item.Locked) DrawRect(rect.Grow(-1), _colour, filled: false, width: 2);
        else DrawRect(rect, _colour * new Color(1, 1, 1, 0.55f), filled: false, width: 1);

        if (Item.UpgradeLevel > 0)
        {
            DrawString(font, new Vector2(4, 13), $"+{Item.UpgradeLevel}", HorizontalAlignment.Left, -1, 11, new Color("f0c96a"));
        }
    }

    public override Variant _GetDragData(Vector2 atPosition) =>
        Item is null || DragFrom is null ? default : DragFrom(this, atPosition);

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        CanTake?.Invoke(GetGlobalTransform() * atPosition, data) ?? false;

    public override void _DropData(Vector2 atPosition, Variant data) =>
        Take?.Invoke(GetGlobalTransform() * atPosition, data);
}
