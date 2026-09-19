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

    private void Toggle()
    {
        Visible = !Visible;

        if (Visible) Refresh();
    }

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

        left.AddChild(Heading("Equipped"));

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

        header.AddChild(Heading("Bag"));

        _yangLabel = new Label();
        _yangLabel.AddThemeColorOverride("font_color", new Color("f0c96a"));
        header.AddChild(_yangLabel);

        var sort = new Button { Text = "Sort" };
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
            Text = "Click an item to equip or unequip it · I to close · U for the anvil",
        };

        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        right.AddChild(hint);
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);

        return label;
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

        _yangLabel.Text = $"{_inventory.Bag.Yang:N0} yang";

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
                Text = $"{slot,-9} {(item is null ? "—" : GameItems.NameOf(item))}",
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
                TooltipText = ItemText.Tooltip(placed.Item),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ClipText = true,
            };

            button.AddThemeFontSizeOverride("font_size", 11);
            button.AddThemeColorOverride("font_color", colour);
            button.AddThemeStyleboxOverride("normal", Background(colour * new Color(1, 1, 1, 0.18f)));

            var item = placed.Item;
            button.Pressed += () =>
            {
                if (spec?.IsEquipment == true) _inventory.Equip(item);

                Refresh();
            };

            _gridRoot.AddChild(button);
            _itemNodes.Add(button);
        }
    }

    private void RefreshStats()
    {
        var player = GetTree().GetFirstNodeInGroup("player");
        var combatant = player?.GetNodeOrNull<Combat.Combatant>("Combatant");

        if (combatant is null) return;

        var stats = combatant.Stats;

        _statsLabel.Text =
            $"\nAttack power {stats.AttackPower:0}\n"
            + $"Defence {stats.Defense:0}\n"
            + $"Health {stats.MaxHp:0}\n"
            + $"Crit {stats.CritChance:P0} · pierce {stats.PierceChance:P0}";
    }
}
