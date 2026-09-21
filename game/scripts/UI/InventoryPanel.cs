using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The bag and the worn gear, on I.
/// </summary>
/// <remarks>
/// Built in code like the rest of the Phase 2–4 UI: this is scaffolding for judging the
/// systems, not the shipped interface (that is Phase 8). Items are drawn at their true grid
/// footprint rather than as uniform cells, because the footprint is a real part of the
/// decision — a two-by-three breastplate costing six cells is something the player weighs.
/// </remarks>
public partial class InventoryPanel : CanvasLayer
{
    private const int CellSize = 52;
    private const int CellGap = 3;

    private PlayerInventory? _inventory;
    private Control _root = null!;
    private Control _gridRoot = null!;
    private VBoxContainer _gearList = null!;
    private Label _yangLabel = null!;
    private Label _statsLabel = null!;
    private Label _notice = null!;
    private readonly List<Control> _itemNodes = [];

    public override void _Ready()
    {
        Layer = 20;
        Build();
        Visible = false;
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        var player = GetTree().GetFirstNodeInGroup("player");

        _inventory = player?.GetNodeOrNull<PlayerInventory>("PlayerInventory");

        if (_inventory is null) return;

        _inventory.Changed += Refresh;
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.ToggleInventory))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed(GameActions.Cancel))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    private bool _counted;

    private void Toggle()
    {
        Visible = !Visible;
        UiState.SetOpen(ref _counted, Visible);

        if (Visible) Refresh();
    }

    // A panel freed while open would otherwise leave the modal count raised forever, and the
    // player could never move again.
    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    // ------------------------------------------------------------------ build

    private void Build()
    {
        _root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };

        _root.AddThemeStyleboxOverride("panel", Background(new Color(0.07f, 0.08f, 0.10f, 0.96f)));
        AddChild(_root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        _root.AddChild(margin);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 22);
        margin.AddChild(columns);

        // -- worn gear
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        left.AddThemeConstantOverride("separation", 6);
        columns.AddChild(left);

        left.AddChild(Heading(L10n.T("Equipped")));

        _gearList = new VBoxContainer();
        _gearList.AddThemeConstantOverride("separation", 4);
        left.AddChild(_gearList);

        _statsLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _statsLabel.AddThemeFontSizeOverride("font_size", 13);
        _statsLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.82f));
        left.AddChild(_statsLabel);

        // -- bag
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 8);
        columns.AddChild(right);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        right.AddChild(header);

        header.AddChild(Heading(L10n.T("Bag")));

        _yangLabel = new Label();
        _yangLabel.AddThemeColorOverride("font_color", new Color("f0c96a"));
        header.AddChild(_yangLabel);

        var sort = new Button { Text = L10n.T("Sort") };
        sort.Pressed += () =>
        {
            _inventory?.Bag.AutoSort();
            Refresh();
        };

        header.AddChild(sort);

        _gridRoot = new Control();
        right.AddChild(_gridRoot);

        var hint = new Label
        {
            Text = L10n.T("Click to equip or unequip · right-click to drop on the ground") + "\n"
                + L10n.T("Shift + right-click destroys it · Ctrl + right-click locks it against both") + "\n"
                + L10n.T("I to close · U for the anvil"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        right.AddChild(hint);

        _notice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _notice.AddThemeFontSizeOverride("font_size", 13);
        _notice.AddThemeColorOverride("font_color", new Color("f0c96a"));
        right.AddChild(_notice);
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);

        return label;
    }

    /// <summary>A protected item: the same fill, with its rarity colour drawn as a hard border.</summary>
    private static StyleBoxFlat Locked(Color colour)
    {
        var style = Background(colour * new Color(1, 1, 1, 0.18f));

        style.BorderColor = colour;
        style.BorderWidthTop = 2;
        style.BorderWidthBottom = 2;
        style.BorderWidthLeft = 2;
        style.BorderWidthRight = 2;

        return style;
    }

    private static StyleBoxFlat Background(Color colour) => new()
    {
        BgColor = colour,
        BorderColor = new Color(0.25f, 0.28f, 0.34f),
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        if (_inventory is null || !Visible) return;

        _yangLabel.Text = L10n.F("{0:N0} yang", _inventory.Bag.Yang);

        RefreshGear();
        RefreshGrid();
        RefreshStats();
    }

    private void RefreshGear()
    {
        foreach (var child in _gearList.GetChildren()) child.QueueFree();

        foreach (var slot in System.Enum.GetValues<EquipSlot>())
        {
            var item = _inventory!.Gear.In(slot);

            var button = new Button
            {
                Text = $"{Words.Of(slot),-9} {(item is null ? "—" : GameItems.NameOf(item))}",
                Alignment = HorizontalAlignment.Left,
                Disabled = item is null,
                TooltipText = item is null ? "" : ItemText.Tooltip(item),
            };

            if (item is not null)
            {
                var spec = GameItems.Spec(item.DefId);
                button.AddThemeColorOverride("font_color", World.LootDrop.RarityColour(spec?.Rarity ?? Rarity.Common));

                var captured = slot;
                button.Pressed += () =>
                {
                    _inventory.Unequip(captured);
                    Refresh();
                };
            }

            _gearList.AddChild(button);
        }
    }

    private void RefreshGrid()
    {
        foreach (var node in _itemNodes) node.QueueFree();

        _itemNodes.Clear();

        var bag = _inventory!.Bag;
        var width = (bag.Width * (CellSize + CellGap)) - CellGap;
        var height = (bag.Height * (CellSize + CellGap)) - CellGap;

        _gridRoot.CustomMinimumSize = new Vector2(width, height);

        // Empty cells, drawn once each refresh. Cheap enough at 80 cells that pooling them
        // would be optimisation without a measurement behind it.
        for (var y = 0; y < bag.Height; y++)
        {
            for (var x = 0; x < bag.Width; x++)
            {
                var cell = new Panel
                {
                    Position = new Vector2(x * (CellSize + CellGap), y * (CellSize + CellGap)),
                    Size = new Vector2(CellSize, CellSize),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };

                cell.AddThemeStyleboxOverride("panel", Background(new Color(0.12f, 0.13f, 0.16f, 0.9f)));
                _gridRoot.AddChild(cell);
                _itemNodes.Add(cell);
            }
        }

        foreach (var placed in bag.Items)
        {
            var spec = GameItems.Spec(placed.Item.DefId);
            var colour = World.LootDrop.RarityColour(spec?.Rarity ?? Rarity.Common);

            var button = new Button
            {
                Position = new Vector2(placed.X * (CellSize + CellGap), placed.Y * (CellSize + CellGap)),
                Size = new Vector2(
                    (placed.Width * (CellSize + CellGap)) - CellGap,
                    (placed.Height * (CellSize + CellGap)) - CellGap),
                Text = ItemText.Label(placed.Item),

                // Compared against whatever occupies the slot it would go into, so the
                // tooltip answers "should I wear this" rather than only "what is this".
                TooltipText = ItemText.Tooltip(placed.Item, WornRival(spec)),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ClipText = true,
            };

            var item = placed.Item;

            button.AddThemeFontSizeOverride("font_size", 11);
            button.AddThemeColorOverride("font_color", colour);

            // A locked item is outlined rather than dimmed: it is protected, not unavailable,
            // and dimming it would read as "you cannot use this".
            button.AddThemeStyleboxOverride("normal", item.Locked
                ? Locked(colour)
                : Background(colour * new Color(1, 1, 1, 0.18f)));

            if (item.Locked) button.Text = "🔒 " + button.Text;

            button.Pressed += () =>
            {
                if (spec?.IsEquipment == true) _inventory.Equip(item);

                Refresh();
            };

            button.GuiInput += @event => OnItemInput(@event, item, button);

            _gridRoot.AddChild(button);
            _itemNodes.Add(button);
        }
    }

    // ------------------------------------------------------------------ throwing things away

    /// <summary>
    /// Right-click, and its two modifiers (ITM-14).
    /// </summary>
    /// <remarks>
    /// The three actions are deliberately asymmetric in how much they ask of the player.
    /// Dropping is reversible — the item is at their feet — so it happens immediately.
    /// Destroying is not, so it always asks, every time, however worthless the item looks:
    /// a rule about which items are worth confirming is a rule that will one day be wrong
    /// about someone's stack of upgrade materials.
    /// <para>
    /// Locking is the standing answer for anything the player never wants to be asked about
    /// again, and it refuses both actions rather than confirming harder.
    /// </para>
    /// </remarks>
    private void OnItemInput(InputEvent @event, ItemInstance item, Control source)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click) return;

        source.AcceptEvent();

        if (click.CtrlPressed)
        {
            item.Locked = !item.Locked;
            Refresh();

            return;
        }

        if (item.Locked)
        {
            Notify(L10n.F("{0} is locked. Ctrl + right-click to unlock it.", GameItems.NameOf(item)));
            return;
        }

        if (click.ShiftPressed)
        {
            ConfirmDestroy(item);
            return;
        }

        Drop(item);
    }

    /// <summary>Puts the item on the ground beside the player, where it can be picked up again.</summary>
    private void Drop(ItemInstance item)
    {
        if (_inventory is null) return;

        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player)
        {
            Notify(L10n.T("Nowhere to drop it."));
            return;
        }

        if (!_inventory.Bag.Remove(item))
        {
            Notify(L10n.T("That item is no longer in the bag."));
            return;
        }

        // Far enough out that it is visible beside the player rather than under them, and
        // close enough that one step recovers it.
        var facing = -player.GlobalTransform.Basis.Z;
        var offset = facing.LengthSquared() > 0.01f ? facing.Normalized() : Vector3.Forward;

        World.LootDrop.Place(GetTree().CurrentScene, item, player.GlobalPosition + (offset * 1.8f));

        Notify(L10n.F("Dropped {0}.", GameItems.NameOf(item)));
        Refresh();
    }

    private void ConfirmDestroy(ItemInstance item)
    {
        var dialog = new ConfirmationDialog
        {
            Title = L10n.T("Destroy item"),
            DialogText = L10n.F("Destroy {0}?", GameItems.NameOf(item) + (item.Count > 1 ? $" x{item.Count}" : ""))
                + "\n\n" + L10n.T("This cannot be undone. To keep it but free the space, right-click to drop it instead."),
            OkButtonText = L10n.T("Destroy"),
            CancelButtonText = L10n.T("Cancel"),
        };

        dialog.Confirmed += () =>
        {
            if (_inventory?.Bag.Remove(item) == true)
            {
                Notify(L10n.F("Destroyed {0}.", GameItems.NameOf(item)));
                Refresh();
            }

            dialog.QueueFree();
        };

        dialog.Canceled += dialog.QueueFree;

        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void Notify(string message)
    {
        _notice.Text = message;
        _noticeFor = 3.5;
        _notice.Modulate = new Color(1, 1, 1, 1);
    }

    public override void _Process(double delta)
    {
        if (_noticeFor <= 0) return;

        _noticeFor -= delta;

        if (_noticeFor < 1.0) _notice.Modulate = new Color(1, 1, 1, (float)Mathf.Max(0, _noticeFor));
        if (_noticeFor <= 0) _notice.Text = "";
    }

    private double _noticeFor;

    /// <summary>
    /// What this item would be replacing. For a ring, the second slot when it is free — the
    /// honest comparison is against what you would actually lose, and you lose nothing by
    /// filling an empty finger.
    /// </summary>
    private ItemInstance? WornRival(ItemSpec? spec)
    {
        if (spec?.Slot is not { } slot || _inventory is null) return null;

        if (slot is EquipSlot.Ring1 or EquipSlot.Ring2)
        {
            if (_inventory.Gear.In(EquipSlot.Ring1) is null || _inventory.Gear.In(EquipSlot.Ring2) is null) return null;
        }

        return _inventory.Gear.In(slot);
    }

    private void RefreshStats()
    {
        var player = GetTree().GetFirstNodeInGroup("player");
        var combatant = player?.GetNodeOrNull<Combat.Combatant>("Combatant");

        if (combatant is null) return;

        var stats = combatant.Stats;

        _statsLabel.Text =
            "\n" + L10n.F("Attack power {0:0}", stats.AttackPower) + "\n"
            + L10n.F("Defence {0:0}", stats.Defense) + "\n"
            + L10n.F("Health {0:0}", stats.MaxHp) + "\n"
            + L10n.F("Crit {0:P0} · pierce {1:P0}", stats.CritChance, stats.PierceChance);
    }
}
