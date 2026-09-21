using System.Linq;
using Godot;
using Kiln.Core.Economy;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Data.Definitions;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// Buying and selling (ITM-10, FR-10.3, FR-12.2 "vendor").
/// </summary>
/// <remarks>
/// Lists rather than a second grid. The merchant's side is three short lists — what is always
/// there, what is on the shelf this level, and what the player just sold — and the player's
/// side is their bag, one row per stack with what it would fetch. Every price is on the row,
/// and every button says what it will do before it is pressed: a shop that makes the player
/// hover to find out a price is a shop that sells things by accident.
/// </remarks>
public partial class MerchantPanel : CanvasLayer
{
    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Dim = new(0.62f, 0.62f, 0.6f);
    private static readonly Color Bad = new(0.9f, 0.45f, 0.4f);

    private PlayerInventory? _inventory;
    private Vendor? _vendor;
    private bool _counted;

    private Label _title = null!;
    private Label _greeting = null!;
    private Label _yang = null!;
    private VBoxContainer _stock = null!;
    private VBoxContainer _bag = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        Layer = 21;
        Build();
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (_inventory is not null) _inventory.Changed -= Refresh;

        UiState.SetOpen(ref _counted, false);
    }

    public void Open(NpcDef npc, Vendor vendor, string greeting)
    {
        if (_inventory is null)
        {
            _inventory = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerInventory>("PlayerInventory");

            if (_inventory is not null) _inventory.Changed += Refresh;
        }

        _vendor = vendor;
        _title.Text = string.IsNullOrEmpty(npc.Title)
            ? GameItems.Localise(npc.Name)
            : $"{GameItems.Localise(npc.Name)}  ·  {GameItems.Localise(npc.Title)}";
        _greeting.Text = greeting;
        _status.Text = "";

        Visible = true;
        UiState.SetOpen(ref _counted, true);
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        UiState.SetOpen(ref _counted, false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        if (@event.IsActionPressed(GameActions.Cancel) || @event.IsActionPressed(GameActions.Interact))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ------------------------------------------------------------------ trading

    private void Buy(VendorOffer offer, int count)
    {
        if (_vendor is null || _inventory is null) return;

        var result = _vendor.Buy(offer, _inventory.Bag, GameItems.Factory, count);
        var name = GameItems.NameOfId(offer.ItemId);

        Say(result switch
        {
            TradeResult.Done => count > 1 ? L10n.F("Bought {0} × {1}.", count, name) : L10n.F("Bought {0}.", name),
            TradeResult.TooPoor => L10n.T("Not enough yang."),
            TradeResult.NoRoom => L10n.T("No room in the bag."),
            _ => L10n.T("That is no longer for sale."),
        }, result == TradeResult.Done);

        if (result == TradeResult.Done) Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndTrade);

        Refresh();
    }

    private void Sell(ItemInstance item)
    {
        if (_vendor is null || _inventory is null) return;

        var name = GameItems.NameOf(item);
        var before = _inventory.Bag.Yang;
        var result = _vendor.Sell(item, _inventory.Bag);

        Say(result switch
        {
            TradeResult.Done => L10n.F("Sold {0} for {1:N0} yang. It can be bought back for a while.", name, _inventory.Bag.Yang - before),
            TradeResult.Locked => L10n.F("{0} is locked. Unlock it in the bag first.", name),
            TradeResult.Worthless => L10n.F("Nobody pays for {0}.", name),
            _ => L10n.F("{0} is not in the bag.", name),
        }, result == TradeResult.Done);

        if (result == TradeResult.Done) Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndTrade);

        Refresh();
    }

    private void Say(string text, bool good)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", good ? new Color(0.55f, 0.82f, 0.58f) : Bad);
    }

    // ------------------------------------------------------------------ lists

    private bool _pending;

    /// <summary>Rebuilds the lists at the end of the frame.</summary>
    /// <remarks>
    /// Deferred, because it is called from inside a button's own press, and the rebuild frees
    /// that button. A node taken out of the tree while it is still emitting is an engine error.
    /// </remarks>
    private void Refresh()
    {
        if (_pending) return;

        _pending = true;
        CallDeferred(nameof(Rebuild));
    }

    private void Rebuild()
    {
        _pending = false;

        if (!Visible || _vendor is null || _inventory is null) return;

        var yang = _inventory.Bag.Yang;

        _yang.Text = L10n.F("{0:N0} yang", yang);

        Clear(_stock);

        Section(_stock, L10n.T("Always in stock"));

        foreach (var offer in _vendor.Staples)
        {
            var row = Row(_stock, offer.ItemId, null, L10n.F("{0:N0}", offer.Price), offer.Price <= yang ? Gold : Bad);

            Button(row, L10n.T("Buy"), offer.Price <= yang, () => Buy(offer, 1));
            Button(row, L10n.T("×10"), offer.Price * 10 <= yang, () => Buy(offer, 10));
        }

        Section(_stock, L10n.T("On the shelf  ·  new stock every level"));

        if (_vendor.Shelf.Count == 0) Note(_stock, L10n.T("Sold out until your next level."));

        foreach (var offer in _vendor.Shelf)
        {
            var row = Row(_stock, offer.ItemId, offer.Item, L10n.F("{0:N0}", offer.Price), offer.Price <= yang ? Gold : Bad);

            Button(row, L10n.T("Buy"), offer.Price <= yang, () => Buy(offer, 1));
        }

        if (_vendor.Buyback.Count > 0)
        {
            Section(_stock, L10n.T("Buy back"));

            foreach (var offer in _vendor.Buyback)
            {
                var row = Row(_stock, offer.ItemId, offer.Item, L10n.F("{0:N0}", offer.Price), offer.Price <= yang ? Gold : Bad);

                Button(row, L10n.T("Buy back"), offer.Price <= yang, () => Buy(offer, 1));
            }
        }

        Clear(_bag);

        var carried = _inventory.Bag.Items
            .OrderBy(p => p.Y)
            .ThenBy(p => p.X)
            .Select(p => p.Item)
            .ToList();

        if (carried.Count == 0) Note(_bag, L10n.T("The bag is empty. Worn gear is not for sale here."));

        foreach (var item in carried)
        {
            var spec = GameItems.Spec(item.DefId);
            var price = spec is null ? 0 : Vendor.SellPrice(spec, item);
            var row = Row(_bag, item.DefId, item, price > 0 ? L10n.F("{0:N0}", price) : "—", price > 0 ? Gold : Dim);

            if (item.Locked) Note(row, L10n.T("locked"));
            else Button(row, L10n.T("Sell"), price > 0, () => Sell(item));
        }
    }

    private static void Clear(Node list)
    {
        foreach (var child in list.GetChildren())
        {
            list.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void Section(VBoxContainer list, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.90f));

        if (list.GetChildCount() > 0) list.AddChild(new HSeparator());

        list.AddChild(label);
    }

    private static void Note(Container parent, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Dim);
        parent.AddChild(label);
    }

    /// <summary>One line of a list: the item, its price, and room for the buttons.</summary>
    private HBoxContainer Row(VBoxContainer list, string itemId, ItemInstance? item, string price, Color priceColour)
    {
        var spec = GameItems.Spec(itemId);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var name = item is null ? GameItems.NameOfId(itemId) : GameItems.NameOf(item);

        if (item is { Count: > 1 }) name += $"  ×{item.Count}";

        var label = new Label
        {
            Text = name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = item is null ? GameItems.NameOfId(itemId) : ItemText.Tooltip(item, Worn(spec)),
        };

        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", World.LootDrop.RarityColour(spec?.Rarity ?? Rarity.Common));
        row.AddChild(label);

        if (spec is { LevelReq: > 1 } && spec.IsEquipment)
        {
            var level = new Label { Text = L10n.F("Lv {0}", spec.LevelReq), CustomMinimumSize = new Vector2(44, 0) };
            level.AddThemeFontSizeOverride("font_size", 12);
            level.AddThemeColorOverride("font_color",
                spec.LevelReq > PlayerProfile.Progression.Level ? Bad : Dim);
            row.AddChild(level);
        }

        var cost = new Label
        {
            Text = price,
            CustomMinimumSize = new Vector2(70, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        cost.AddThemeFontSizeOverride("font_size", 13);
        cost.AddThemeColorOverride("font_color", priceColour);
        row.AddChild(cost);

        list.AddChild(row);

        return row;
    }

    private static void Button(HBoxContainer row, string text, bool enabled, System.Action pressed)
    {
        var button = new Button
        {
            Text = text,
            Disabled = !enabled,
            CustomMinimumSize = new Vector2(text.Length > 4 ? 76 : 48, 0),
            FocusMode = Control.FocusModeEnum.None,
        };

        button.AddThemeFontSizeOverride("font_size", 12);
        button.Pressed += pressed;
        row.AddChild(button);
    }

    private ItemInstance? Worn(ItemSpec? spec)
    {
        if (spec?.Slot is not { } slot || _inventory is null) return null;

        return _inventory.Gear.In(slot);
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };

        root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.10f, 0.97f),
            BorderColor = new Color(0.25f, 0.28f, 0.34f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 16,
            ContentMarginBottom = 14,
        });

        AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        root.AddChild(column);

        var header = new HBoxContainer();
        column.AddChild(header);

        _title = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", Gold);
        header.AddChild(_title);

        _yang = new Label();
        _yang.AddThemeFontSizeOverride("font_size", 16);
        _yang.AddThemeColorOverride("font_color", Gold);
        header.AddChild(_yang);

        _greeting = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(860, 0) };
        _greeting.AddThemeFontSizeOverride("font_size", 13);
        _greeting.AddThemeColorOverride("font_color", new Color(0.82f, 0.8f, 0.74f));
        column.AddChild(_greeting);

        column.AddChild(new HSeparator());

        var sides = new HBoxContainer();
        sides.AddThemeConstantOverride("separation", 24);
        column.AddChild(sides);

        _stock = Side(sides, L10n.T("For sale"));
        _bag = Side(sides, L10n.T("Your bag  ·  sells for a quarter of its value"));

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_status);

        var hint = new Label { Text = L10n.T("Hover a name for details   ·   Esc or F to close") };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        column.AddChild(hint);
    }

    private static VBoxContainer Side(HBoxContainer sides, string heading)
    {
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        side.AddThemeConstantOverride("separation", 6);
        sides.AddChild(side);

        var label = new Label { Text = heading };
        label.AddThemeFontSizeOverride("font_size", 15);
        side.AddChild(label);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(420, 380),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        side.AddChild(scroll);

        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        return list;
    }
}
