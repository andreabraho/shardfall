using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The anvil, the socket bench and the reroll table, on U.
/// </summary>
/// <remarks>
/// The upgrade panel is where the redesign's central promise is either kept or broken, so it
/// says everything out loud: the real chance, the pity counter and how many failures remain
/// before the next attempt is guaranteed, and what the attempt costs against what the player
/// actually holds. Metin2's version hides all of this behind a percentage and an animation,
/// which is precisely how a system that can delete your item stays tolerable — and exactly
/// what this design refuses to inherit.
/// </remarks>
public partial class WorkbenchPanel : CanvasLayer
{
    private PlayerInventory? _inventory;
    private ItemInstance? _selected;

    private VBoxContainer _itemList = null!;
    private Label _title = null!;
    private RichTextLabel _detail = null!;
    private Label _chance = null!;
    private Label _pity = null!;
    private Label _cost = null!;
    private Button _upgrade = null!;
    private Button _bore = null!;
    private VBoxContainer _stoneList = null!;
    private Label _rerollCost = null!;
    private Button _reroll = null!;
    private VBoxContainer _lockList = null!;
    private Label _status = null!;

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

    /// <summary>Opens the bench, as a bench in the world does (FR-7.14).</summary>
    public void Open()
    {
        if (Visible) return;

        Visible = true;
        UiState.SetOpen(ref _counted, true);
        _selected ??= FirstUpgradable();
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.ToggleUpgradeBench))
        {
            if (Visible)
            {
                Visible = false;
                UiState.SetOpen(ref _counted, false);
            }
            else
            {
                Open();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed(GameActions.Cancel))
        {
            Visible = false;
            UiState.SetOpen(ref _counted, false);
            GetViewport().SetInputAsHandled();
        }
    }

    private bool _counted;

    // A panel freed while open would otherwise leave the modal count raised forever, and the
    // player could never move again.
    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    private ItemInstance? FirstUpgradable()
    {
        if (_inventory is null) return null;

        foreach (var slot in System.Enum.GetValues<EquipSlot>())
        {
            if (_inventory.Gear.In(slot) is { } worn && GameItems.Spec(worn.DefId)?.IsUpgradable == true) return worn;
        }

        return _inventory.Bag.Items
            .Select(p => p.Item)
            .FirstOrDefault(i => GameItems.Spec(i.DefId)?.IsUpgradable == true);
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

        root.AddThemeStyleboxOverride("panel", Panel(new Color(0.07f, 0.08f, 0.10f, 0.97f)));
        AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(margin);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 24);
        margin.AddChild(columns);

        // -- what can be worked on
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(240, 380) };
        left.AddThemeConstantOverride("separation", 6);
        columns.AddChild(left);
        left.AddChild(Heading("Workbench"));

        _itemList = new VBoxContainer();
        _itemList.AddThemeConstantOverride("separation", 4);
        left.AddChild(_itemList);

        // -- the item and the three benches
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
        right.AddThemeConstantOverride("separation", 10);
        columns.AddChild(right);

        _title = Heading("—");
        right.AddChild(_title);

        _detail = new RichTextLabel
        {
            CustomMinimumSize = new Vector2(0, 120),
            BbcodeEnabled = false,
            FitContent = true,
        };

        _detail.AddThemeFontSizeOverride("normal_font_size", 13);
        right.AddChild(_detail);

        right.AddChild(Separator());
        right.AddChild(Heading2("Upgrade"));

        _chance = new Label();
        _chance.AddThemeFontSizeOverride("font_size", 15);
        right.AddChild(_chance);

        _pity = new Label();
        _pity.AddThemeColorOverride("font_color", new Color("8fd3a8"));
        right.AddChild(_pity);

        _cost = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _cost.AddThemeFontSizeOverride("font_size", 13);
        right.AddChild(_cost);

        _upgrade = new Button { Text = "Upgrade" };
        _upgrade.Pressed += DoUpgrade;
        right.AddChild(_upgrade);

        var safety = new Label
        {
            Text = "Failure costs the materials. It never destroys or downgrades the item.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        safety.AddThemeFontSizeOverride("font_size", 12);
        safety.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.64f));
        right.AddChild(safety);

        right.AddChild(Separator());
        right.AddChild(Heading2("Sockets"));

        _bore = new Button { Text = "Open a socket" };
        _bore.Pressed += DoBore;
        right.AddChild(_bore);

        _stoneList = new VBoxContainer();
        _stoneList.AddThemeConstantOverride("separation", 3);
        right.AddChild(_stoneList);

        right.AddChild(Separator());
        right.AddChild(Heading2("Bonus lines"));

        _lockList = new VBoxContainer();
        _lockList.AddThemeConstantOverride("separation", 3);
        right.AddChild(_lockList);

        _rerollCost = new Label();
        _rerollCost.AddThemeFontSizeOverride("font_size", 13);
        right.AddChild(_rerollCost);

        _reroll = new Button { Text = "Reroll" };
        _reroll.Pressed += DoReroll;
        right.AddChild(_reroll);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", 13);
        right.AddChild(_status);

        var hint = new Label { Text = "U to close" };
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

    private static Label Heading2(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.90f));

        return label;
    }

    private static HSeparator Separator() => new();

    private static StyleBoxFlat Panel(Color colour) => new()
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

    // ------------------------------------------------------------------ actions

    private void DoUpgrade()
    {
        if (_inventory is null || _selected is null) return;

        var ladder = GameItems.Catalogue.LadderFor(_selected);
        var before = _selected.UpgradeLevel;

        var result = UpgradeAnvil.Attempt(_selected, ladder, _inventory.Bag, GameItems.CraftRng);

        _status.Text = result.Outcome switch
        {
            UpgradeOutcome.Success when result.WasGuaranteed => $"Guaranteed success — now +{result.Level}.",
            UpgradeOutcome.Success => $"Success — now +{result.Level}.",
            UpgradeOutcome.Failed => $"Failed. Still +{before}, and the next attempt is closer to guaranteed.",
            UpgradeOutcome.CannotAfford => "Not enough yang or materials.",
            UpgradeOutcome.AtMaxLevel => "Already at the highest level.",
            _ => "This item cannot be upgraded.",
        };

        _status.AddThemeColorOverride("font_color", result.Outcome switch
        {
            UpgradeOutcome.Success => new Color("8fd3a8"),
            UpgradeOutcome.Failed => new Color("d8b06a"),
            _ => new Color("c98b8b"),
        });

        _inventory.ApplyToStats();
        Refresh();
    }

    private void DoBore()
    {
        if (_inventory is null || _selected is null) return;

        var outcome = SocketBench.TryBore(_selected, _inventory.Bag);

        _status.Text = outcome switch
        {
            SocketOutcome.Success => "A socket is open.",
            SocketOutcome.NoSocketsLeft => "Every socket on this item is already open.",
            SocketOutcome.CannotAfford => "Needs a Boring Stone and yang.",
            _ => outcome.ToString(),
        };

        _inventory.ApplyToStats();
        Refresh();
    }

    private void DoSlot(string stoneId, int socketIndex)
    {
        if (_inventory is null || _selected is null) return;

        var outcome = SocketBench.TrySlot(_selected, socketIndex, stoneId, GameItems.Catalogue, _inventory.Bag);

        _status.Text = outcome == SocketOutcome.Success ? "Stone set." : outcome.ToString();

        _inventory.ApplyToStats();
        Refresh();
    }

    private void DoRemoveStone(int socketIndex)
    {
        if (_inventory is null || _selected is null) return;

        var outcome = SocketBench.TryRemove(_selected, socketIndex, _inventory.Bag);

        _status.Text = outcome switch
        {
            SocketOutcome.Success => "Stone recovered, intact.",
            SocketOutcome.NoRoomForStone => "No room in the bag for the stone.",
            SocketOutcome.CannotAfford => $"Removal costs {ItemEconomy.SocketRemoveYang:N0} yang.",
            _ => outcome.ToString(),
        };

        _inventory.ApplyToStats();
        Refresh();
    }

    private void DoReroll()
    {
        if (_inventory is null || _selected is null) return;

        var spec = GameItems.Spec(_selected.DefId);

        if (spec is null) return;

        var pool = GameItems.Catalogue.Pool(spec.BonusPoolId);
        var outcome = RerollTable.TryReroll(_selected, spec, pool, _inventory.Bag, GameItems.CraftRng);

        _status.Text = outcome switch
        {
            RerollOutcome.Success => "Rerolled.",
            RerollOutcome.CannotAfford => "Needs Mutation Ink and yang.",
            RerollOutcome.NothingToReroll => "Nothing left to reroll — unlock a line first.",
            RerollOutcome.TooManyLocked => "Too many locked lines for this item's rarity.",
            _ => "This item has no bonus pool.",
        };

        _inventory.ApplyToStats();
        Refresh();
    }

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        if (_inventory is null || !Visible) return;

        RefreshItemList();

        if (_selected is null)
        {
            _title.Text = "Nothing to work on";
            _detail.Text = "";
            _upgrade.Disabled = true;
            _bore.Disabled = true;
            _reroll.Disabled = true;

            return;
        }

        _title.Text = GameItems.NameOf(_selected);
        _detail.Text = ItemText.Tooltip(_selected);

        RefreshUpgrade();
        RefreshSockets();
        RefreshReroll();
    }

    private void RefreshItemList()
    {
        foreach (var child in _itemList.GetChildren()) child.QueueFree();

        // Worn first, then the bag — and each row carries which it is (ITM-15). The bench is
        // where the player decides what to improve, and that decision is mostly "is this the
        // thing I am actually wearing?". Leaving it to be inferred from the order made them
        // hold the list's shape in their head to read a single row.
        var candidates = new List<(ItemInstance Item, EquipSlot? Worn)>();

        foreach (var slot in System.Enum.GetValues<EquipSlot>())
        {
            if (_inventory!.Gear.In(slot) is { } worn) candidates.Add((worn, slot));
        }

        candidates.AddRange(_inventory!.Bag.Items
            .Select(p => p.Item)
            .Where(i => GameItems.Spec(i.DefId)?.IsEquipment == true)
            .Select(i => (i, (EquipSlot?)null)));

        foreach (var (item, worn) in candidates)
        {
            var spec = GameItems.Spec(item.DefId);
            var rarity = spec?.Rarity ?? Rarity.Common;

            var button = new Button
            {
                Text = worn is null
                    ? GameItems.NameOf(item)
                    : $"{GameItems.NameOf(item)}   ·   {ItemIcon.NameOf(worn.Value)}",
                Alignment = HorizontalAlignment.Left,
                TooltipText = ItemText.Tooltip(item),
                Disabled = ReferenceEquals(item, _selected),
                Icon = ItemIcon.For(rarity, worn is not null),
                ExpandIcon = false,
            };

            button.AddThemeColorOverride("font_color", World.LootDrop.RarityColour(rarity));

            var captured = item;
            button.Pressed += () =>
            {
                _selected = captured;
                _status.Text = "";
                Refresh();
            };

            _itemList.AddChild(button);
        }

        if (candidates.Count == 0) _selected = null;
        else if (_selected is not null && !candidates.Any(c => ReferenceEquals(c.Item, _selected)))
        {
            _selected = candidates[0].Item;
        }
    }

    private void RefreshUpgrade()
    {
        var ladder = GameItems.Catalogue.LadderFor(_selected!);
        var quote = UpgradeAnvil.Quote(_selected!, ladder, _inventory!.Bag);

        if (quote is null)
        {
            _chance.Text = ladder is null ? "This item does not upgrade." : "Already at the highest level.";
            _pity.Text = "";
            _cost.Text = "";
            _upgrade.Disabled = true;

            return;
        }

        _chance.Text = $"+{_selected!.UpgradeLevel} → +{quote.Step.To}   {quote.DisplayedChance:P0}"
            + (quote.Guaranteed ? "  (guaranteed)" : "");

        // The counter is the whole point: the player can always see the ladder converging.
        _pity.Text = quote.Guaranteed
            ? "Pity reached — this attempt cannot fail."
            : quote.Step.Pity > 0
                ? $"Failed {quote.FailuresSoFar} in a row · guaranteed after {quote.AttemptsToGuarantee} more"
                : "";

        _cost.Text = $"Cost: {quote.Step.Yang:N0} yang{DescribeMaterials(quote.Step.Materials)}";
        _upgrade.Disabled = !quote.Affordable;
    }

    private string DescribeMaterials(IReadOnlyDictionary<string, int> materials)
    {
        if (materials.Count == 0) return "";

        var parts = materials.Select(m =>
        {
            var have = _inventory!.Bag.CountOf(m.Key);
            var name = GameContent.IsLoaded && GameContent.Database.Items.TryGetValue(m.Key, out var def)
                ? GameItems.Localise(def.Name)
                : m.Key;

            return $"{name} {have}/{m.Value}";
        });

        return " · " + string.Join(" · ", parts);
    }

    private void RefreshSockets()
    {
        foreach (var child in _stoneList.GetChildren()) child.QueueFree();

        var item = _selected!;

        _bore.Disabled = SocketBench.NextClosedSocket(item) is null
            || !_inventory!.Bag.CanAfford(ItemEconomy.SocketBoreYang,
                new Dictionary<string, int> { [SocketBench.BoringStoneId] = 1 });

        _bore.Text = item.Sockets.Count == 0
            ? "This item has no sockets"
            : $"Open a socket — {ItemEconomy.SocketBoreYang:N0} yang + 1 Boring Stone";

        for (var i = 0; i < item.Sockets.Count; i++)
        {
            var socket = item.Sockets[i];
            var index = i;

            if (!socket.IsOpen)
            {
                _stoneList.AddChild(Muted($"Socket {i + 1}: sealed"));
                continue;
            }

            if (socket.StoneId is { } stoneId)
            {
                var remove = new Button
                {
                    Text = $"Socket {i + 1}: {NameOfItem(stoneId)} — remove for {ItemEconomy.SocketRemoveYang:N0} yang",
                    Alignment = HorizontalAlignment.Left,
                };

                remove.Pressed += () => DoRemoveStone(index);
                _stoneList.AddChild(remove);
                continue;
            }

            // An open, empty socket offers whatever stones the player is carrying.
            var stones = _inventory!.Bag.Items
                .Select(p => p.Item.DefId)
                .Distinct()
                .Where(id => GameItems.Spec(id) is { IsEquipment: false, Grants.Count: > 0 })
                .ToList();

            if (stones.Count == 0)
            {
                _stoneList.AddChild(Muted($"Socket {i + 1}: empty — no stones in the bag"));
                continue;
            }

            foreach (var candidate in stones)
            {
                var button = new Button
                {
                    Text = $"Socket {i + 1}: set {NameOfItem(candidate)}",
                    Alignment = HorizontalAlignment.Left,
                };

                var captured = candidate;
                button.Pressed += () => DoSlot(captured, index);
                _stoneList.AddChild(button);
            }
        }
    }

    private void RefreshReroll()
    {
        foreach (var child in _lockList.GetChildren()) child.QueueFree();

        var item = _selected!;
        var spec = GameItems.Spec(item.DefId);

        if (spec is null || item.Bonuses.Count == 0)
        {
            _rerollCost.Text = "This item has no bonus lines.";
            _reroll.Disabled = true;

            return;
        }

        var max = RerollTable.MaxLockedLines(spec);

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

            toggle.Toggled += pressed =>
            {
                item.SetLineLocked(index, pressed);
                Refresh();
            };

            _lockList.AddChild(toggle);
        }

        var quote = RerollTable.Quote(item, spec, _inventory!.Bag);

        _rerollCost.Text =
            $"Lock up to {max} line{(max == 1 ? "" : "s")} · cost {quote.Yang:N0} yang{DescribeMaterials(quote.Materials)}";

        _reroll.Disabled = !quote.Affordable || item.LockedLineCount >= item.Bonuses.Count;
    }

    private static Label Muted(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.58f, 0.62f, 0.68f));

        return label;
    }

    private static string NameOfItem(string id) =>
        GameContent.IsLoaded && GameContent.Database.Items.TryGetValue(id, out var def)
            ? GameItems.Localise(def.Name)
            : id;
}
