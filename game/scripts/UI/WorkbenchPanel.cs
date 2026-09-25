using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The smith (REF-11): upgrading, sockets and rerolling, at a smith or a bench only.
/// </summary>
/// <remarks>
/// Laid out as the original's smith window: it opens beside the bag, an item is carried from
/// the bag onto the anvil — or right-clicked there — and the window shows what the next level
/// gives, the chance, and every material as its icon with how many are held against how many
/// are needed. Three tabs share the anvil: upgrade, sockets, bonus lines.
/// <para>
/// No failure ever destroys or downgrades an item; that line stays on the window because it
/// is what makes the chance something a player can take.
/// </para>
/// </remarks>
public partial class WorkbenchPanel : CanvasLayer
{
    private enum Tab { Upgrade, Sockets, Reroll }

    private const int IconSize = 44;

    /// <summary>The smith window while it is open, so the bag can hand items to it.</summary>
    public static WorkbenchPanel? Current { get; private set; }

    private PlayerInventory? _inventory;
    private ItemInstance? _selected;
    private Tab _tab = Tab.Upgrade;

    /// <summary>The bench the window was opened at, when it holds the tower smith's free upgrade.</summary>
    private World.BenchNode? _bench;

    private Control _root = null!;
    private ItemView _anvil = null!;
    private Label _title = null!;
    private Label _hint = null!;
    private readonly List<Button> _tabs = [];
    private VBoxContainer _body = null!;
    private Label _status = null!;
    private ItemTip _tip = null!;
    private bool _blessed;

    public override void _Ready()
    {
        Layer = 21;
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
    }

    /// <summary>Opens the smith, at a smith or a bench in the world (FR-7.14), and the bag beside it.</summary>
    public void Open(World.BenchNode? bench = null)
    {
        if (Visible) return;

        _bench = bench;
        Visible = true;
        Current = this;
        UiState.SetOpen(ref _counted, true);

        // What is worked on comes from the bag, so the bag opens with the window.
        GetParent()?.GetNodeOrNull<InventoryPanel>("InventoryPanel")?.Open();

        // Still owned, it stays on the anvil; otherwise the anvil starts empty.
        if (_selected is not null && !Owned(_selected)) _selected = null;

        _status.Text = "";
        Refresh();
    }

    private void Close()
    {
        if (!Visible) return;

        Visible = false;
        Current = null;
        _tip.Clear();
        UiState.SetOpen(ref _counted, false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed(GameActions.Cancel)) return;

        Close();
        GetViewport().SetInputAsHandled();
    }

    private bool _counted;

    // A panel freed while open would otherwise leave the modal count raised forever, and the
    // player could never move again.
    public override void _ExitTree()
    {
        if (Current == this) Current = null;

        UiState.SetOpen(ref _counted, false);
    }

    /// <summary>
    /// An item right-clicked in the bag while the window is open (REF-11): gear goes on the
    /// anvil; a stone, on the sockets tab, into the first empty socket. False leaves the click
    /// to the bag.
    /// </summary>
    public bool Offer(ItemInstance item)
    {
        var spec = GameItems.Spec(item.DefId);

        if (spec is null) return false;

        if (spec.IsEquipment)
        {
            Place(item);
            return true;
        }

        if (_tab == Tab.Sockets && _selected is not null && IsStone(spec))
        {
            var empty = _selected.Sockets.Select((s, i) => (s, i)).FirstOrDefault(p => p.s.IsOpen && p.s.StoneId is null);

            if (empty.s is not null) SetStone(item.DefId, empty.i);
            else Say(L10n.T("No open, empty socket on this item."), Bad);

            return true;
        }

        return false;
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        // Beside the bag, which keeps the right edge (REF-10): the item goes from one to the other.
        _root = new PanelContainer
        {
            AnchorLeft = 1f,
            AnchorTop = 0.5f,
            AnchorRight = 1f,
            AnchorBottom = 0.5f,
            OffsetRight = -304,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.Both,
            CustomMinimumSize = new Vector2(340, 0),
        };

        // Moved by holding its border or title and dragging (REF-19).
        PanelMover.Attach(_root, "workbench");

        _root.AddThemeStyleboxOverride("panel", Style(new Color(0.07f, 0.08f, 0.10f, 0.97f)));
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

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 4);
        column.AddChild(tabs);

        foreach (var (tab, name) in new[] { (Tab.Upgrade, L10n.T("Upgrade")), (Tab.Sockets, L10n.T("Sockets")), (Tab.Reroll, L10n.T("Bonus lines")) })
        {
            var button = new Button { Text = name, ToggleMode = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var captured = tab;

            button.Pressed += () =>
            {
                _tab = captured;
                _status.Text = "";
                Refresh();
            };

            tabs.AddChild(button);
            _tabs.Add(button);
        }

        // -- the anvil
        var anvilRow = new HBoxContainer();
        anvilRow.AddThemeConstantOverride("separation", 12);
        column.AddChild(anvilRow);

        _anvil = new ItemView
        {
            Placeholder = L10n.T("Anvil"),
            CustomMinimumSize = new Vector2(92, 92),
            CanTake = (_, data) => ItemOf(data) is { } item && GameItems.Spec(item.DefId)?.IsEquipment == true,
            Take = (_, data) =>
            {
                if (ItemOf(data) is { } item) Place(item);
            },
        };

        _anvil.MouseEntered += () => Describe(_anvil.Item);
        _anvil.MouseExited += () => _tip.Clear();
        _anvil.GuiInput += @event =>
        {
            // Right-click takes the item back off the anvil.
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && _selected is not null)
            {
                _anvil.AcceptEvent();
                _selected = null;
                _tip.Clear();
                Refresh();
            }
        };

        anvilRow.AddChild(_anvil);

        _hint = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _hint.AddThemeFontSizeOverride("font_size", 13);
        anvilRow.AddChild(_hint);

        column.AddChild(new HSeparator());

        _body = new VBoxContainer();
        _body.AddThemeConstantOverride("separation", 8);
        column.AddChild(_body);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(310, 0) };
        _status.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_status);

        var footer = Muted(L10n.T("Esc to close"));
        column.AddChild(footer);

        _tip = new ItemTip();
        AddChild(_tip);
    }

    private static StyleBoxFlat Style(Color colour) => new()
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

    private static Label Muted(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(310, 0) };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.58f, 0.62f, 0.68f));

        return label;
    }

    private static Label Line(string text, int size = 14, Color? colour = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(310, 0) };
        label.AddThemeFontSizeOverride("font_size", size);
        if (colour is { } c) label.AddThemeColorOverride("font_color", c);

        return label;
    }

    private static readonly Color Good = new("8fd3a8");
    private static readonly Color Warn = new("d8b06a");
    private static readonly Color Bad = new("e07a6a");
    private static readonly Color Gold = new("ffd36b");

    // ------------------------------------------------------------------ the anvil

    private void Place(ItemInstance item)
    {
        _selected = item;
        _status.Text = "";
        _blessed = false;
        Refresh();
    }

    private bool Owned(ItemInstance item) =>
        _inventory is not null
        && (_inventory.Bag.Find(item) is not null || System.Enum.GetValues<EquipSlot>().Any(s => ReferenceEquals(_inventory.Gear.In(s), item)));

    /// <summary>The item a drag from the bag or the worn figure carries, found by its id.</summary>
    private ItemInstance? ItemOf(Variant data)
    {
        if (_inventory is null || data.VariantType != Variant.Type.Dictionary) return null;

        var payload = data.AsGodotDictionary();

        if (!payload.TryGetValue("kind", out var kind) || kind.AsString() != "kiln_item") return null;
        if (!payload.TryGetValue("uid", out var uidValue)) return null;

        var uid = uidValue.AsInt64();

        return _inventory.Bag.Items.Select(p => p.Item).FirstOrDefault(i => i.Uid == uid)
            ?? System.Enum.GetValues<EquipSlot>().Select(s => _inventory.Gear.In(s)).FirstOrDefault(i => i?.Uid == uid);
    }

    private static bool IsStone(ItemSpec spec) => spec is { IsEquipment: false, Grants.Count: > 0 };

    private void Describe(ItemInstance? item)
    {
        if (item is null)
        {
            _tip.Clear();
            return;
        }

        _tip.Say(ItemText.RichTooltip(item, null, PlayerProfile.Progression.Level));
    }

    private void Say(string message, Color colour)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", colour);
    }

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        if (_inventory is null || !Visible) return;

        if (_selected is not null && !Owned(_selected)) _selected = null;

        _title.Text = _bench is { HasGift: true } && IsInstanceValid(_bench) ? L10n.T("Tower smith") : L10n.T("Smith");

        for (var i = 0; i < _tabs.Count; i++) _tabs[i].SetPressedNoSignal(i == (int)_tab);

        _anvil.Display(_selected);

        _hint.Text = _selected is null
            ? L10n.T("Drag an item from the bag onto the anvil, or right-click it there.")
            : GameItems.NameOf(_selected);

        _hint.AddThemeColorOverride("font_color", _selected is null
            ? new Color(0.62f, 0.66f, 0.72f)
            : World.LootDrop.RarityColour(GameItems.Spec(_selected.DefId)?.Rarity ?? Rarity.Common));

        foreach (var child in _body.GetChildren()) child.QueueFree();

        if (_selected is null) return;

        switch (_tab)
        {
            case Tab.Upgrade: BuildUpgrade(_selected); break;
            case Tab.Sockets: BuildSockets(_selected); break;
            default: BuildReroll(_selected); break;
        }
    }

    /// <summary>A row of material icons, each with how many are held against how many it takes, and the yang.</summary>
    private Control Costs(long yang, IReadOnlyDictionary<string, int> materials)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        foreach (var (id, need) in materials)
        {
            var cell = new VBoxContainer();
            cell.AddThemeConstantOverride("separation", 2);

            var icon = new ItemView { CustomMinimumSize = new Vector2(IconSize, IconSize), MouseFilter = Control.MouseFilterEnum.Pass };
            var shown = new ItemInstance(0, id, need);
            icon.Display(shown);
            icon.MouseEntered += () => Describe(shown);
            icon.MouseExited += () => _tip.Clear();
            cell.AddChild(icon);

            var have = _inventory!.Bag.CountOf(id);
            var count = new Label { Text = $"{have}/{need}", HorizontalAlignment = HorizontalAlignment.Center };
            count.AddThemeFontSizeOverride("font_size", 12);
            count.AddThemeColorOverride("font_color", have >= need ? Good : Bad);
            cell.AddChild(count);

            row.AddChild(cell);
        }

        var money = Coin.Amount(yang, 14, _inventory!.Bag.Yang >= yang ? new Color("f0c96a") : Bad);
        money.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(money);

        return row;
    }

    // ------------------------------------------------------------------ upgrade

    private void BuildUpgrade(ItemInstance item)
    {
        var spec = GameItems.Spec(item.DefId);
        var ladder = GameItems.Catalogue.LadderFor(item);
        var scrolls = _inventory!.Bag.CountOf(UpgradeAnvil.BlessingScrollId);

        if (scrolls == 0) _blessed = false;

        var quote = UpgradeAnvil.Quote(item, ladder, _inventory.Bag, _blessed);

        if (quote is null || spec is null)
        {
            _body.AddChild(Line(ladder is null ? L10n.T("This item does not upgrade.") : L10n.T("Already at the highest level."), 15, Warn));
            return;
        }

        var chance = quote.DisplayedChance;

        _body.AddChild(Line(L10n.F("+{0}  →  +{1}", item.UpgradeLevel, quote.Step.To), 20, Gold));
        _body.AddChild(Line(L10n.F("Chance {0:P0}", chance) + (quote.Guaranteed ? "  " + L10n.T("(guaranteed)") : ""), 16,
            chance >= 0.999 ? Good : chance >= 0.5 ? Warn : Bad));

        // The counter is the whole point: the player can always see the ladder converging.
        if (quote.Guaranteed) _body.AddChild(Line(L10n.T("Pity reached — this attempt cannot fail."), 13, Good));
        else if (quote.Step.Pity > 0)
        {
            _body.AddChild(Line(L10n.F("Failed {0} in a row · guaranteed after {1} more", quote.FailuresSoFar, quote.AttemptsToGuarantee), 13, Good));
        }

        // What the next level gives.
        var now = UpgradeScaling.BonusPercent(item.UpgradeLevel);
        var next = UpgradeScaling.BonusPercent(quote.Step.To);

        var scale = UpgradeScaling.Multiplier(quote.Step.To);

        if (spec.WeaponDamageMax > 0)
        {
            _body.AddChild(Line(L10n.F("Damage {0:0}–{1:0}  →  {2:0}–{3:0}",
                item.WeaponDamageMin(spec), item.WeaponDamageMax(spec), spec.WeaponDamageMin * scale, spec.WeaponDamageMax * scale)));
        }
        else if (spec.ArmorValue > 0)
        {
            _body.AddChild(Line(L10n.F("Armour {0:0}  →  {1:0}", item.ArmorValue(spec), spec.ArmorValue * scale)));
        }

        _body.AddChild(Line(L10n.F("Base stats +{0:0}%  →  +{1:0}%", now, next), 13, new Color(0.72f, 0.76f, 0.82f)));

        _body.AddChild(Costs(quote.Step.Yang, quote.Step.Materials));

        // The scroll, while there is one and the attempt is not sure already.
        var bless = new CheckBox
        {
            Text = L10n.F("Read a Blessing Scroll: +{0:P0} ({1} held)", UpgradeAnvil.BlessingBonus, scrolls),
            ButtonPressed = _blessed,
            Disabled = scrolls == 0 || quote.Guaranteed,
        };

        bless.Toggled += on =>
        {
            _blessed = on;
            Refresh();
        };

        _body.AddChild(bless);

        var upgrade = new Button { Text = L10n.T("Upgrade"), Disabled = !quote.Affordable };
        upgrade.Pressed += () => DoUpgrade(item, ladder);
        _body.AddChild(upgrade);

        // The tower smith's gift: shown only at a bench that holds one.
        if (_bench is { HasGift: true } && IsInstanceValid(_bench))
        {
            var gift = new Button { Text = L10n.T("Free upgrade — the tower smith") };
            gift.AddThemeColorOverride("font_color", Gold);
            gift.Pressed += () => DoGift(item, ladder);
            _body.AddChild(gift);
        }

        _body.AddChild(Muted(L10n.T("Failure costs the materials. It never destroys or downgrades the item.")));
    }

    private void DoUpgrade(ItemInstance item, UpgradeLadder? ladder)
    {
        if (_inventory is null) return;

        var before = item.UpgradeLevel;
        var result = UpgradeAnvil.Attempt(item, ladder, _inventory.Bag, GameItems.CraftRng, _blessed);

        if (result.Attempted)
        {
            Audio.AudioDirector.Play(result.Outcome == UpgradeOutcome.Success ? Kiln.Data.Ids.Sounds.SndUpgradeSuccess : Kiln.Data.Ids.Sounds.SndUpgradeFail);

            // Written at once (REF-18), so the outcome cannot be undone by loading the save
            // from before the attempt.
            Saving.SaveService.Autosave($"Upgraded {item.DefId}");
        }

        Say(result.Outcome switch
        {
            UpgradeOutcome.Success when result.WasGuaranteed => L10n.F("Guaranteed success — now +{0}.", result.Level),
            UpgradeOutcome.Success => L10n.F("Success — now +{0}.", result.Level),
            UpgradeOutcome.Failed => L10n.F("Failed. Still +{0}, and the next attempt is closer to guaranteed.", before),
            UpgradeOutcome.CannotAfford => L10n.T("Not enough gan or materials."),
            UpgradeOutcome.AtMaxLevel => L10n.T("Already at the highest level."),
            _ => L10n.T("This item cannot be upgraded."),
        }, result.Outcome switch
        {
            UpgradeOutcome.Success => Good,
            UpgradeOutcome.Failed => Warn,
            _ => Bad,
        });

        _inventory.ApplyToStats();
        Refresh();
    }

    private void DoGift(ItemInstance item, UpgradeLadder? ladder)
    {
        if (_inventory is null || _bench is not { HasGift: true } bench || !IsInstanceValid(bench)) return;

        var result = UpgradeAnvil.Gift(item, ladder);

        if (result.Outcome != UpgradeOutcome.Success) return;

        bench.Revoke();
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndUpgradeSuccess);
        GD.Print($"[tower] the smith upgraded {item.DefId} to +{result.Level}");

        Say(L10n.F("The smith's gift — now +{0}.", result.Level), Gold);
        Saving.SaveService.Autosave($"Gift on {item.DefId}");

        _inventory.ApplyToStats();
        Refresh();
    }

    // ------------------------------------------------------------------ sockets

    private void BuildSockets(ItemInstance item)
    {
        if (item.Sockets.Count == 0)
        {
            _body.AddChild(Line(L10n.T("This item has no sockets."), 15, Warn));
            return;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _body.AddChild(row);

        for (var i = 0; i < item.Sockets.Count; i++)
        {
            var socket = item.Sockets[i];
            var index = i;

            var view = new ItemView
            {
                CustomMinimumSize = new Vector2(56, 56),
                Placeholder = !socket.IsOpen ? L10n.T("Sealed") : socket.StoneId is null ? L10n.T("Empty") : "",
                CanTake = (_, data) => socket.IsOpen && socket.StoneId is null
                    && ItemOf(data) is { } stone && GameItems.Spec(stone.DefId) is { } s && IsStone(s),
                Take = (_, data) =>
                {
                    if (ItemOf(data) is { } stone) SetStone(stone.DefId, index);
                },
            };

            var shown = socket.StoneId is { } id ? new ItemInstance(0, id) : null;
            view.Display(shown);

            view.MouseEntered += () => Describe(shown);
            view.MouseExited += () => _tip.Clear();
            view.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && socket.StoneId is not null)
                {
                    view.AcceptEvent();
                    RemoveStone(index);
                }
            };

            row.AddChild(view);
        }

        _body.AddChild(Muted(L10n.F("Drag a stone from the bag onto an empty socket, or right-click it in the bag. Right-click a set stone to take it out for {0:N0} gan — it comes back intact.", ItemEconomy.SocketRemoveYang)));

        if (SocketBench.NextClosedSocket(item) is null) return;

        var cost = new Dictionary<string, int> { [SocketBench.BoringStoneId] = 1 };

        _body.AddChild(Line(L10n.T("Open the next socket"), 14));
        _body.AddChild(Costs(ItemEconomy.SocketBoreYang, cost));

        var bore = new Button { Text = L10n.T("Open a socket"), Disabled = !_inventory!.Bag.CanAfford(ItemEconomy.SocketBoreYang, cost) };
        bore.Pressed += () => DoBore(item);
        _body.AddChild(bore);
    }

    private void DoBore(ItemInstance item)
    {
        if (_inventory is null) return;

        var outcome = SocketBench.TryBore(item, _inventory.Bag);

        if (outcome == SocketOutcome.Success) Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndSocket);

        Say(outcome switch
        {
            SocketOutcome.Success => L10n.T("A socket is open."),
            SocketOutcome.NoSocketsLeft => L10n.T("Every socket on this item is already open."),
            SocketOutcome.CannotAfford => L10n.T("Needs a Boring Stone and gan."),
            _ => outcome.ToString(),
        }, outcome == SocketOutcome.Success ? Good : Bad);

        _inventory.ApplyToStats();
        Refresh();
    }

    private void SetStone(string stoneId, int socketIndex)
    {
        if (_inventory is null || _selected is null) return;

        var outcome = SocketBench.TrySlot(_selected, socketIndex, stoneId, GameItems.Catalogue, _inventory.Bag);

        if (outcome == SocketOutcome.Success) Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndSocket);

        Say(outcome == SocketOutcome.Success ? L10n.T("Stone set.") : L10n.T("The stone could not be set."),
            outcome == SocketOutcome.Success ? Good : Bad);

        _inventory.ApplyToStats();
        Refresh();
    }

    private void RemoveStone(int socketIndex)
    {
        if (_inventory is null || _selected is null) return;

        var outcome = SocketBench.TryRemove(_selected, socketIndex, _inventory.Bag);

        if (outcome == SocketOutcome.Success) Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndSocket);

        Say(outcome switch
        {
            SocketOutcome.Success => L10n.T("Stone recovered, intact."),
            SocketOutcome.NoRoomForStone => L10n.T("No room in the bag for the stone."),
            SocketOutcome.CannotAfford => L10n.F("Removal costs {0:N0} gan.", ItemEconomy.SocketRemoveYang),
            _ => outcome.ToString(),
        }, outcome == SocketOutcome.Success ? Good : Bad);

        _inventory.ApplyToStats();
        Refresh();
    }

    // ------------------------------------------------------------------ bonus lines

    private void BuildReroll(ItemInstance item)
    {
        var spec = GameItems.Spec(item.DefId);

        if (spec is null || item.Bonuses.Count == 0)
        {
            _body.AddChild(Line(L10n.T("This item has no bonus lines."), 15, Warn));
            return;
        }

        var max = RerollTable.MaxLockedLines(spec);

        _body.AddChild(Muted(max == 1 ? L10n.T("Lock up to 1 line: it is kept, and the others are rolled again.") : L10n.F("Lock up to {0} lines: they are kept, and the others are rolled again.", max)));

        for (var i = 0; i < item.Bonuses.Count; i++)
        {
            var line = item.Bonuses[i];
            var index = i;

            var toggle = new CheckBox
            {
                Text = line.Describe(),
                ButtonPressed = line.Locked,
                Disabled = !line.Locked && item.LockedLineCount >= max,
            };

            toggle.AddThemeColorOverride("font_color", Good);
            toggle.Toggled += pressed =>
            {
                item.SetLineLocked(index, pressed);
                Refresh();
            };

            _body.AddChild(toggle);
        }

        var quote = RerollTable.Quote(item, spec, _inventory!.Bag);

        _body.AddChild(Costs(quote.Yang, quote.Materials));

        var reroll = new Button { Text = L10n.T("Reroll"), Disabled = !quote.Affordable || item.LockedLineCount >= item.Bonuses.Count };
        reroll.Pressed += () => DoReroll(item, spec);
        _body.AddChild(reroll);
    }

    private void DoReroll(ItemInstance item, ItemSpec spec)
    {
        if (_inventory is null) return;

        var pool = GameItems.Catalogue.Pool(spec.BonusPoolId);
        var outcome = RerollTable.TryReroll(item, spec, pool, _inventory.Bag, GameItems.CraftRng);

        if (outcome == RerollOutcome.Success)
        {
            Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndReroll);
            Saving.SaveService.Autosave($"Rerolled {item.DefId}");
        }

        Say(outcome switch
        {
            RerollOutcome.Success => L10n.T("Rerolled."),
            RerollOutcome.CannotAfford => L10n.T("Needs Mutation Ink and gan."),
            RerollOutcome.NothingToReroll => L10n.T("Nothing left to reroll — unlock a line first."),
            RerollOutcome.TooManyLocked => L10n.T("Too many locked lines for this item's rarity."),
            _ => L10n.T("This item has no bonus pool."),
        }, outcome == RerollOutcome.Success ? Good : Bad);

        _inventory.ApplyToStats();
        Refresh();
    }
}
