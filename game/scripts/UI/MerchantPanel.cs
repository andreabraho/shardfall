using System.Collections.Generic;
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
/// Buying and selling (ITM-10, FR-10.3), laid out as the original's shop (REF-12).
/// </summary>
/// <remarks>
/// The merchant's goods are a grid of icons beside the bag, each with its price and coin under
/// it: right-click buys one, Shift + right-click ten of what is always in stock. Selling is
/// carrying an item from the bag onto the shop. What was just sold waits in a row of its own
/// to be bought back at the price it fetched, so a sale is never a mistake that cannot be
/// undone. The price is under every icon, never only in a tooltip: a shop that makes the
/// player hover to find out a price is a shop that sells things by accident.
/// </remarks>
public partial class MerchantPanel : CanvasLayer
{
    private static readonly Color Gold = new("f0c96a");
    private static readonly Color Dim = new(0.62f, 0.62f, 0.6f);
    private static readonly Color Bad = new(0.9f, 0.45f, 0.4f);
    private static readonly Color Good = new(0.55f, 0.82f, 0.58f);

    private const int Cell = 50;
    private const int Columns = 5;

    private PlayerInventory? _inventory;
    private Vendor? _vendor;
    private bool _counted;

    private Control _root = null!;
    private Label _title = null!;
    private Label _greeting = null!;
    private HBoxContainer _purse = null!;
    private VBoxContainer _stock = null!;
    private Label _status = null!;
    private ItemTip _tip = null!;

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

        // Selling is carrying from the bag, so the bag opens with the shop.
        GetParent()?.GetNodeOrNull<InventoryPanel>("InventoryPanel")?.Open();

        Refresh();
    }

    public void Close()
    {
        Visible = false;
        _tip.Clear();
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

    // ------------------------------------------------------------------ build

    private void Build()
    {
        // Beside the bag, which keeps the right edge (REF-10).
        _root = new PanelContainer
        {
            AnchorLeft = 1f,
            AnchorTop = 0.5f,
            AnchorRight = 1f,
            AnchorBottom = 0.5f,
            OffsetRight = -304,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.Both,
        };

        _root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.10f, 0.97f),
            BorderColor = new Color(0.25f, 0.28f, 0.34f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
        });

        AddChild(_root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _root.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 17);
        column.AddChild(_title);

        _greeting = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Columns * (Cell + 12), 0) };
        _greeting.AddThemeFontSizeOverride("font_size", 13);
        _greeting.AddThemeColorOverride("font_color", Dim);
        column.AddChild(_greeting);

        // The whole list is where a carried item is sold: let go of it anywhere over the goods.
        var drop = new ShopDrop
        {
            CanTake = data => ItemOf(data) is not null,
            Take = data =>
            {
                if (ItemOf(data) is { } item) Sell(item);
            },
        };

        column.AddChild(drop);

        _stock = new VBoxContainer();
        _stock.AddThemeConstantOverride("separation", 6);
        drop.AddChild(_stock);

        _purse = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        column.AddChild(_purse);

        var hint = new Label
        {
            Text = L10n.T("Right-click to buy · Shift + right-click buys ten · drag from the bag onto the shop to sell"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(Columns * (Cell + 12), 0),
        };

        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        column.AddChild(hint);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Columns * (Cell + 12), 0) };
        _status.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_status);

        _tip = new ItemTip();
        AddChild(_tip);
    }

    /// <summary>The item a drag from the bag carries, found by its id; only what is in the bag sells.</summary>
    private ItemInstance? ItemOf(Variant data)
    {
        if (_inventory is null || data.VariantType != Variant.Type.Dictionary) return null;

        var payload = data.AsGodotDictionary();

        if (!payload.TryGetValue("kind", out var kind) || kind.AsString() != "kiln_item") return null;
        if (!payload.TryGetValue("uid", out var uid)) return null;

        return _inventory.Bag.Items.Select(p => p.Item).FirstOrDefault(i => i.Uid == uid.AsInt64());
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
            TradeResult.TooPoor => L10n.T("Not enough gan."),
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
            TradeResult.Done => L10n.F("Sold {0} for {1:N0} gan. It can be bought back for a while.", name, _inventory.Bag.Yang - before),
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
        _status.AddThemeColorOverride("font_color", good ? Good : Bad);
    }

    // ------------------------------------------------------------------ the goods

    private bool _pending;

    /// <summary>Rebuilds the goods at the end of the frame.</summary>
    /// <remarks>
    /// Deferred, because it is called from inside a cell's own input, and the rebuild frees
    /// that cell. A node taken out of the tree while it is still emitting is an engine error.
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

        _tip.Clear();

        foreach (var child in _purse.GetChildren()) child.QueueFree();
        _purse.AddChild(Coin.Amount(_inventory.Bag.Yang, 15, Gold));

        foreach (var child in _stock.GetChildren())
        {
            _stock.RemoveChild(child);
            child.QueueFree();
        }

        Grid(L10n.T("Always in stock"), _vendor.Staples, staple: true);

        if (_vendor.Shelf.Count == 0) Section(L10n.T("Sold out until your next level."), Dim);
        else Grid(L10n.T("On the shelf  ·  new stock every level"), _vendor.Shelf, staple: false);

        if (_vendor.Buyback.Count > 0) Grid(L10n.T("Buy back"), _vendor.Buyback, staple: false);
    }

    private void Section(string text, Color colour)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", colour);
        _stock.AddChild(label);
    }

    /// <summary>A heading and its offers as a grid of icons, each with its price and coin under it.</summary>
    private void Grid(string heading, IEnumerable<VendorOffer> offers, bool staple)
    {
        Section(heading, new Color(0.78f, 0.82f, 0.90f));

        var grid = new GridContainer { Columns = Columns };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 6);
        _stock.AddChild(grid);

        var gan = _inventory!.Bag.Yang;

        foreach (var offer in offers)
        {
            var cell = new VBoxContainer();
            cell.AddThemeConstantOverride("separation", 2);

            var shown = offer.Item ?? new ItemInstance(0, offer.ItemId);
            // A drop on an icon sells too: a cell that stops the mouse does not pass drops on.
            var view = new ItemView
            {
                CustomMinimumSize = new Vector2(Cell, Cell),
                CanTake = (_, data) => ItemOf(data) is not null,
                Take = (_, data) =>
                {
                    if (ItemOf(data) is { } item) Sell(item);
                },
            };

            view.Display(shown);

            var spec = GameItems.Spec(offer.ItemId);
            view.MouseEntered += () => _tip.Say(ItemText.RichTooltip(shown, Worn(spec), PlayerProfile.Progression.Level));
            view.MouseExited += () => _tip.Clear();
            view.GuiInput += @event =>
            {
                if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click) return;

                view.AcceptEvent();
                Buy(offer, staple && click.ShiftPressed ? 10 : 1);
            };

            cell.AddChild(view);

            var price = Coin.Amount(offer.Price, 11, offer.Price <= gan ? Gold : Bad);
            price.Alignment = BoxContainer.AlignmentMode.Center;
            price.CustomMinimumSize = new Vector2(Cell, 0);

            // Just the number under an icon: the coin already says what it is.
            if (price.GetChild(1) is Label label) label.Text = L10n.F("{0:N0}", offer.Price);

            cell.AddChild(price);
            grid.AddChild(cell);
        }
    }

    private ItemInstance? Worn(ItemSpec? spec)
    {
        if (spec?.Slot is not { } slot || _inventory is null) return null;

        return _inventory.Gear.In(slot);
    }
}
