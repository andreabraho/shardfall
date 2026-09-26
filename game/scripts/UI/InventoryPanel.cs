using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The bag and the worn gear, on I — laid out as the original lays them out (REF-10).
/// </summary>
/// <remarks>
/// The worn pieces stand on a figure at the top, the bag is two pages of five columns by nine
/// rows under it, and everything is moved by carrying it: drag within the bag to rearrange,
/// onto the figure to wear, off the figure to take off, out of the window to drop it on the
/// ground. Right-click wears or uses. Items are drawn at their true grid footprint, because
/// the footprint is a real part of the decision — a two-by-three breastplate costing six
/// cells is something the player weighs.
/// </remarks>
public partial class InventoryPanel : CanvasLayer
{
    private const int CellSize = 46;
    private const int CellGap = 3;
    private const int Step = CellSize + CellGap;

    /// <summary>
    /// Where each worn piece stands on the figure, in squares of the bag's size: the head at
    /// the top, the weapon, body and shield across the middle, hands and feet below.
    /// </summary>
    private static readonly (EquipSlot Slot, int X, int Y, int H)[] Figure =
    [
        (EquipSlot.Earring, 0, 0, 1),
        (EquipSlot.Helmet, 2, 0, 1),
        (EquipSlot.Necklace, 4, 0, 1),
        (EquipSlot.Weapon, 0, 1, 2),
        (EquipSlot.Armor, 2, 1, 2),
        (EquipSlot.Shield, 4, 1, 2),
        (EquipSlot.Bracelet, 0, 3, 1),
        (EquipSlot.Ring1, 1, 3, 1),
        (EquipSlot.Boots, 2, 3, 1),
        (EquipSlot.Ring2, 3, 3, 1),
    ];

    private PlayerInventory? _inventory;
    private Control _root = null!;
    private BagGrid _grid = null!;
    private readonly Dictionary<EquipSlot, ItemView> _worn = [];
    private readonly List<ItemView> _bagViews = [];
    private readonly List<BagPageTab> _tabs = [];
    private ItemTip _tip = null!;
    private HBoxContainer _purse = null!;
    private Label _notice = null!;
    private int _page;

    // What is being carried, while a drag is in the air.
    private ItemInstance? _carried;
    private EquipSlot? _carriedFrom;
    private Vector2I _grab;

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
        UiState.SetSide(ref _counted, _root, Visible);

        if (Visible) Refresh();
        else _tip.Clear();
    }

    /// <summary>Opens the bag if it is closed — the smith opens it beside its window (REF-11).</summary>
    public void Open()
    {
        if (!Visible) Toggle();
    }

    // A side panel (REF-07): play goes on with the bag open. Freed while open, it lets go.
    public override void _ExitTree() => UiState.SetSide(ref _counted, _root, false);

    // ------------------------------------------------------------------ build

    private void Build()
    {
        // At the right edge, not the middle (REF-07): the bag stays open during a fight, and
        // the middle of the screen is where the fight is.
        _root = new PanelContainer
        {
            AnchorLeft = 1f,
            AnchorTop = 0.5f,
            AnchorRight = 1f,
            AnchorBottom = 0.5f,
            OffsetRight = -16,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.Both,
        };

        // Moved by holding its border or title and dragging (REF-19).
        PanelMover.Attach(_root, "inventory");

        _root.AddThemeStyleboxOverride("panel", Background(new Color(0.07f, 0.08f, 0.10f, 0.96f)));
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

        column.AddChild(Heading(L10n.T("Equipped")));

        // -- the figure
        var figure = new Control { CustomMinimumSize = new Vector2((PlayerInventory.Columns * Step) - CellGap, (4 * Step) - CellGap) };
        column.AddChild(figure);

        foreach (var (slot, x, y, h) in Figure)
        {
            var view = new ItemView
            {
                Worn = slot,
                Placeholder = Words.Of(slot),
                Position = new Vector2(x * Step, y * Step),
                Size = new Vector2(CellSize, (h * Step) - CellGap),
            };

            Wire(view);
            figure.AddChild(view);
            _worn[slot] = view;
        }

        // -- page tabs, sort
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 6);
        column.AddChild(bar);

        bar.AddChild(Heading(L10n.T("Bag")));

        for (var page = 0; page < PlayerInventory.Pages; page++)
        {
            var tab = new BagPageTab
            {
                Page = page,
                Text = page == 0 ? "I" : "II",
                ToggleMode = true,
                CustomMinimumSize = new Vector2(36, 0),
                HeldOver = TurnTo,
            };

            var captured = page;
            tab.Pressed += () => TurnTo(captured);

            bar.AddChild(tab);
            _tabs.Add(tab);
        }

        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var sort = new Button { Text = L10n.T("Sort") };
        sort.Pressed += () =>
        {
            _inventory?.Bag.AutoSort();
            Refresh();
        };

        bar.AddChild(sort);

        // -- the page
        _grid = new BagGrid
        {
            Columns = PlayerInventory.Columns,
            Rows = PlayerInventory.PageRows,
            CellSize = CellSize,
            Gap = CellGap,
            CanTake = CanLandInBag,
            Take = LandInBag,
        };

        column.AddChild(_grid);

        // The purse, under the bag, with its coin (REF-12).
        _purse = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        column.AddChild(_purse);

        _hint = new Label
        {
            Text = L10n.T("Drag to move, onto the figure to wear, out of the window to drop") + "\n"
                + L10n.T("Right-click to wear or use · Shift + right-click destroys · Ctrl + right-click locks"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2((PlayerInventory.Columns * Step) - CellGap, 0),
        };

        _hint.AddThemeFontSizeOverride("font_size", 11);
        _hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        column.AddChild(_hint);

        _notice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2((PlayerInventory.Columns * Step) - CellGap, 0) };
        _notice.AddThemeFontSizeOverride("font_size", 13);
        _notice.AddThemeColorOverride("font_color", new Color("f0c96a"));
        column.AddChild(_notice);

        _tip = new ItemTip();
        AddChild(_tip);
    }

    /// <summary>Hooks a view up to carrying, hovering and right-clicking.</summary>
    private void Wire(ItemView view)
    {
        view.DragFrom = StartCarrying;

        if (view.Worn is null)
        {
            view.CanTake = CanLandInBag;
            view.Take = LandInBag;
        }
        else
        {
            view.CanTake = (_, data) => CanWear(view, data);
            view.Take = (_, data) => Wear(view, data);
        }

        view.MouseEntered += () => Describe(view);
        view.MouseExited += () => _tip.Clear();
        view.GuiInput += @event => OnItemInput(@event, view);
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 17);

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

        UiNodes.Clear(_purse);
        _purse.AddChild(Coin.Amount(_inventory.Bag.Yang, 15, new Color("f0c96a")));

        foreach (var (slot, view) in _worn) view.Display(_inventory.Gear.In(slot));

        for (var i = 0; i < _tabs.Count; i++) _tabs[i].SetPressedNoSignal(i == _page);

        RefreshPage();
    }

    private void RefreshPage()
    {
        foreach (var view in _bagViews) view.QueueFree();

        _bagViews.Clear();

        var bag = _inventory!.Bag;
        var top = _page * bag.PageHeight;

        foreach (var placed in bag.Items)
        {
            if (placed.Y < top || placed.Y >= top + bag.PageHeight) continue;

            var rect = _grid.RectOf(placed.X, placed.Y - top, placed.Width, placed.Height);
            var view = new ItemView { Position = rect.Position, Size = rect.Size };

            Wire(view);
            view.Display(placed.Item);

            _grid.AddChild(view);
            _bagViews.Add(view);
        }

        _grid.Raise();
    }

    private void TurnTo(int page)
    {
        if (page == _page || page < 0 || page >= PlayerInventory.Pages) return;

        _page = page;
        _tip.Clear();
        Refresh();
    }

    // ------------------------------------------------------------------ the card

    private void Describe(ItemView view)
    {
        if (view.Item is null || _carried is not null)
        {
            _tip.Clear();
            return;
        }

        // A worn piece is described on its own; a bag item against what it would replace,
        // so the card answers "should I wear this" rather than only "what is this".
        var rival = view.Worn is null ? WornRival(GameItems.Spec(view.Item.DefId)) : null;

        _tip.Say(ItemText.RichTooltip(view.Item, rival, PlayerProfile.Progression.Level));
    }

    // ------------------------------------------------------------------ carrying

    private Variant StartCarrying(ItemView view, Vector2 at)
    {
        if (view.Item is not { } item) return default;

        _carried = item;
        _carriedFrom = view.Worn;

        // Which of the item's squares was picked up, so it lands the way it was held.
        _grab = view.Worn is null
            ? new Vector2I(Mathf.FloorToInt(at.X / Step), Mathf.FloorToInt(at.Y / Step))
            : Vector2I.Zero;

        _tip.Clear();

        var spec = GameItems.Spec(item.DefId);
        var preview = new ItemView
        {
            Size = new Vector2(((spec?.Width ?? 1) * Step) - CellGap, ((spec?.Height ?? 1) * Step) - CellGap),
            Modulate = new Color(1, 1, 1, 0.8f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        preview.Display(item);

        // The preview hangs from the cursor at the point it was picked up.
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        preview.Position = -at;
        holder.AddChild(preview);
        view.SetDragPreview(holder);

        return new Godot.Collections.Dictionary { ["kind"] = "kiln_item", ["uid"] = item.Uid };
    }

    private bool IsOurs(Variant data) =>
        _carried is not null
        && data.VariantType == Variant.Type.Dictionary
        && data.AsGodotDictionary().TryGetValue("kind", out var kind)
        && kind.AsString() == "kiln_item";

    /// <summary>The top-left square an item carried to a screen point would land on, page-wide.</summary>
    private Vector2I LandingAt(Vector2 screen) => _grid.CellAt(screen) - _grab;

    private bool CanLandInBag(Vector2 screen, Variant data)
    {
        if (!IsOurs(data) || _inventory is null || GameItems.Spec(_carried!.DefId) is not { } spec) return false;

        var at = LandingAt(screen);
        var fits = _inventory.Bag.Fits(at.X, at.Y + (_page * _inventory.Bag.PageHeight), spec.Width, spec.Height,
            ignoring: _carriedFrom is null ? _carried : null);

        _grid.Preview(new Rect2I(at, new Vector2I(spec.Width, spec.Height)), fits);

        return fits;
    }

    private void LandInBag(Vector2 screen, Variant data)
    {
        if (!IsOurs(data) || _inventory is null) return;

        var at = LandingAt(screen);
        var y = at.Y + (_page * _inventory.Bag.PageHeight);

        if (_carriedFrom is { } slot)
        {
            if (_inventory.UnequipTo(slot, at.X, y)) _inventory.ApplyToStats();
        }
        else
        {
            _inventory.Bag.TryMove(_carried!, at.X, y);
        }

        Refresh();
    }

    private bool CanWear(ItemView slot, Variant data)
    {
        _grid.Preview(null, false);

        if (!IsOurs(data) || _carriedFrom is not null || slot.Worn is not { } target) return false;

        var wants = GameItems.Spec(_carried!.DefId)?.Slot;

        // A ring goes on either hand.
        return wants == target || (wants is EquipSlot.Ring1 or EquipSlot.Ring2 && target is EquipSlot.Ring1 or EquipSlot.Ring2);
    }

    private void Wear(ItemView slot, Variant data)
    {
        if (!IsOurs(data)) return;

        Equip(_carried!);
    }

    /// <summary>
    /// Where a drag ends. Let go outside the window, an item from the bag is dropped on the
    /// ground at the player's feet — the original's gesture for throwing something away.
    /// Let go anywhere else that did not take it, and it goes back where it was.
    /// </summary>
    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || _carried is null) return;

        var item = _carried;
        var from = _carriedFrom;

        _carried = null;
        _carriedFrom = null;
        _grid.Preview(null, false);

        if (GetViewport().GuiIsDragSuccessful() || !Visible) return;

        var mouse = _root.GetViewport().GetMousePosition();

        if (_root.GetGlobalRect().HasPoint(mouse)) return;

        // With a window open over play — the smith, a merchant — nothing is thrown on the
        // ground by a drag that missed its target: the player is working, not clearing out.
        if (UiState.ModalOpen) return;

        // Worn pieces are taken off first, into the bag; only what is in the bag is dropped.
        if (from is not null) return;

        TryDrop(item);
    }

    // ------------------------------------------------------------------ right-click

    /// <summary>
    /// Right-click, and its two modifiers (ITM-14, REF-10).
    /// </summary>
    /// <remarks>
    /// Plain right-click wears or uses, as in the original. Destroying cannot be undone, so it
    /// always asks, every time, however worthless the item looks: a rule about which items
    /// are worth confirming is a rule that will one day be wrong about someone's stack of
    /// upgrade materials. Locking is the standing answer for anything the player never wants
    /// to be asked about again, and it refuses dropping and destroying rather than
    /// confirming harder.
    /// </remarks>
    private void OnItemInput(InputEvent @event, ItemView view)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click) return;
        if (view.Item is not { } item || _inventory is null) return;

        view.AcceptEvent();

        // A worn piece: right-click takes it off.
        if (view.Worn is { } slot)
        {
            if (!_inventory.Unequip(slot)) Notify(L10n.T("No room in the bag to take it off."));
            else _inventory.ApplyToStats();

            Refresh();
            return;
        }

        if (click.CtrlPressed)
        {
            item.Locked = !item.Locked;
            Refresh();

            return;
        }

        if (click.ShiftPressed)
        {
            if (item.Locked)
            {
                Notify(L10n.F("{0} is locked. Ctrl + right-click to unlock it.", GameItems.NameOf(item)));
                return;
            }

            ConfirmDestroy(item);
            return;
        }

        // At the smith, right-click puts gear on the anvil and a stone in a socket (REF-11).
        if (WorkbenchPanel.Current?.Offer(item) == true)
        {
            Refresh();
            return;
        }

        var spec = GameItems.Spec(item.DefId);

        if (spec?.IsEquipment == true) Equip(item);
        else if (item.DefId == Player.HealthFlask.DraughtId) PourDraught();

        Refresh();
    }

    /// <summary>Wears an item from the bag, and says why when it cannot.</summary>
    private void Equip(ItemInstance item)
    {
        if (_inventory is null) return;

        var spec = GameItems.Spec(item.DefId);
        var outcome = _inventory.Equip(item);

        switch (outcome)
        {
            case EquipOutcome.Equipped:
                _inventory.ApplyToStats();
                break;

            case EquipOutcome.LevelTooLow:
                Notify(L10n.F("Needs level {0}.", spec?.LevelReq ?? 0));
                break;

            case EquipOutcome.WrongClass:
                Notify(L10n.T("The Warrior cannot use this."));
                break;

            default:
                Notify(L10n.T("No room in the bag for what it replaces."));
                break;
        }

        Refresh();
    }

    // ------------------------------------------------------------------ filling the flask

    /// <summary>
    /// Pours one draught into the flask (REF-02) — the only thing that refills it, and the
    /// reason yang is still worth carrying once the gear is bought.
    /// </summary>
    /// <remarks>
    /// Refused while the flask is full, so a mis-click never costs a draught, and for three
    /// seconds after a hit, so it is not a second flask to drink mid-fight.
    /// </remarks>
    private void PourDraught()
    {
        if (_inventory is null) return;

        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player
            || player.GetNodeOrNull<Player.HealthFlask>("HealthFlask") is not { } flask)
        {
            return;
        }

        if (flask.Charges >= flask.MaxCharges)
        {
            Notify(L10n.T("The flask is already full."));
            return;
        }

        // Not in the middle of a fight: three seconds clear of any hit first.
        if (flask.RefillLockedFor > 0)
        {
            Notify(L10n.F("Not while you are being hit. Wait {0:0.0} s.", flask.RefillLockedFor));
            return;
        }

        if (!_inventory.Bag.TryConsume(Player.HealthFlask.DraughtId)) return;

        flask.AddCharge();

        Notify(L10n.F("Flask filled — {0} of {1} charges.", flask.Charges, flask.MaxCharges));
    }

    // ------------------------------------------------------------------ throwing things away

    /// <summary>Puts the item on the ground beside the player, where it can be picked up again.</summary>
    private void TryDrop(ItemInstance item)
    {
        if (_inventory is null) return;

        if (item.Locked)
        {
            Notify(L10n.F("{0} is locked. Ctrl + right-click to unlock it.", GameItems.NameOf(item)));
            return;
        }

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
                + "\n\n" + L10n.T("This cannot be undone. To keep it but free the space, drag it out of the bag to drop it instead."),
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

    private Label _hint = null!;

    /// <summary>
    /// The two lines of how-to under the bag, dropped when they would push the window over the
    /// task bar — at a larger interface size (UIX-04) the bag only fits without them.
    /// </summary>
    private void FitHint()
    {
        if (!IsInstanceValid(_root) || !_root.IsInsideTree()) return;

        var room = _root.GetViewportRect().Size.Y - TaskBar.Height;
        var withHint = _root.Size.Y + (_hint.Visible ? 0 : _hint.GetCombinedMinimumSize().Y + 4);

        var show = withHint <= room;

        if (show == _hint.Visible) return;

        _hint.Visible = show;

        // A container keeps its size when what is in it shrinks: let it go back to what it needs.
        _root.ResetSize();
    }

    public override void _Process(double delta)
    {
        if (Visible) FitHint();

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
}
