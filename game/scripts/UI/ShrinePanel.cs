using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.World;
using Kiln.Game.Input;
using Kiln.Game.Items;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// What a shrine sells: somewhere else to be, and a second opinion about your build
/// (WLD-02, WLD-09, PRG-06).
/// </summary>
/// <remarks>
/// Both services are here rather than at a vendor because both are decisions about the run
/// as a whole, and a shrine is where the game already pauses. Respec in particular belongs
/// somewhere the player passes often and cheaply: a build you cannot change is a build you
/// will not experiment with, and the class only has one interesting choice per few levels
/// as it is.
/// </remarks>
public partial class ShrinePanel : CanvasLayer
{
    private PlayerInventory? _inventory;
    private Player.PlayerCharacter? _character;

    private Label _title = null!;
    private Label _notice = null!;
    private VBoxContainer _destinations = null!;
    private Label _respecCost = null!;
    private Button _respec = null!;
    private Label _status = null!;
    private Label _toast = null!;
    private double _toastFor;

    private string _here = "";
    private bool _counted;


    public override void _Ready()
    {
        Layer = 22;
        Build();

        // The layer itself stays visible: the notice that appears when you touch a shrine has
        // to show while the panel is shut, which is the only time it is any use.
        _panel.Visible = false;
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        var player = GetTree().GetFirstNodeInGroup("player");

        _inventory = player?.GetNodeOrNull<PlayerInventory>("PlayerInventory");
        _character = player?.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter");
    }

    /// <summary>The line that appears when the player touches a shrine, with no panel opening.</summary>
    public void Announce(string label, bool firstTime)
    {
        _toast.Text = firstTime
            ? L10n.F("{0} — shrine discovered.  [F] to travel or respec", label)
            : L10n.F("{0} — rested.  [F] to travel or respec", label);

        _toast.Modulate = new Color(1, 1, 1, 1);
        _toastFor = 4.0;
    }

    public bool IsOpen => _panel.Visible;

    public void Open(string shrineId)
    {
        _here = shrineId;
        _panel.Visible = true;
        UiState.SetOpen(ref _counted, true);
        Refresh();
    }

    private void Close()
    {
        _panel.Visible = false;
        UiState.SetOpen(ref _counted, false);
    }

    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    public override void _Process(double delta)
    {
        if (_toastFor <= 0) return;

        _toastFor -= delta;

        if (_toastFor < 1.0) _toast.Modulate = new Color(1, 1, 1, (float)Mathf.Max(0, _toastFor));
        if (_toastFor <= 0) _toast.Text = "";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || !@event.IsActionPressed(GameActions.Cancel)) return;

        Close();
        GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        _toast = new Label
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.14f,
            AnchorBottom = 0.14f,
            GrowHorizontal = Control.GrowDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        _toast.AddThemeFontSizeOverride("font_size", 17);
        _toast.AddThemeColorOverride("font_color", new Color("ffd88a"));
        _toast.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _toast.AddThemeConstantOverride("outline_size", 6);

        // Lives outside the panel so it shows while the player is walking, which is the only
        // time it is useful.
        var layer = new Control
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        layer.AddChild(_toast);
        AddChild(layer);

        var root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };

        root.AddThemeStyleboxOverride("panel", Panel());
        AddChild(root);
        _panel = root;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(margin);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        _title = Heading(L10n.T("Shrine"));
        column.AddChild(_title);

        _notice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _notice.AddThemeFontSizeOverride("font_size", 12);
        _notice.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.64f));
        column.AddChild(_notice);

        column.AddChild(new HSeparator());
        column.AddChild(Heading2(L10n.T("Travel")));

        _destinations = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _destinations.AddThemeConstantOverride("separation", 4);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 330),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        scroll.AddChild(_destinations);
        column.AddChild(scroll);

        column.AddChild(new HSeparator());
        column.AddChild(Heading2(L10n.T("Reconsider")));

        _respecCost = new Label();
        _respecCost.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_respecCost);

        _respec = new Button { Text = L10n.T("Refund attribute points") };
        _respec.Pressed += DoRespec;
        column.AddChild(_respec);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_status);

        var close = new Button { Text = L10n.T("Close  (Esc)") };
        close.Pressed += Close;
        column.AddChild(close);
    }

    private PanelContainer _panel = null!;

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        if (!GameWorld.IsLoaded) return;

        var shrine = GameWorld.Graph.Shrine(_here);
        var zone = shrine is null ? null : GameWorld.Graph[shrine.Zone];

        _title.Text = shrine is null ? _here : GameItems.Localise(shrine.Name);
        _notice.Text = zone is null
            ? ""
            : L10n.F("{0} · level {1} · {2} shrine(s) found", GameItems.Localise(zone.Name), zone.Band, GameWorld.Travel.Discovered.Count);

        RefreshTravel();
        RefreshRespec();
    }

    private static readonly Color ShrineGold = new("ffd88a");
    private static readonly Color Dim = new(0.55f, 0.58f, 0.62f);
    private static readonly Color Short = new(0.90f, 0.42f, 0.36f);

    /// <summary>
    /// The travel list (REF-14): every shrine found, under the map it stands on, maps in the
    /// order of their levels, each with its level range. The shrine the player stands at is
    /// listed too, greyed, so the list reads as the whole network rather than as "elsewhere".
    /// </summary>
    private void RefreshTravel()
    {
        foreach (var child in _destinations.GetChildren()) child.QueueFree();

        var yang = _inventory?.Bag.Yang ?? 0;
        var inCombat = InCombat();
        var any = false;

        var maps = GameWorld.Travel.Destinations()
            .GroupBy(s => s.Zone)
            .Select(g => (Zone: GameWorld.Graph[g.Key], Shrines: g.ToList()))
            .Where(m => m.Zone is not null)
            .OrderBy(m => m.Zone!.Band.Min)
            .ThenBy(m => m.Zone!.Id, System.StringComparer.Ordinal);

        foreach (var (zone, shrines) in maps)
        {
            _destinations.AddChild(MapHeading(zone!));

            foreach (var shrine in shrines)
            {
                var quote = GameWorld.Travel.Quote(shrine.Id, yang, inCombat);

                if (quote.Refusal != TravelRefusal.AlreadyHere) any = true;

                _destinations.AddChild(Destination(shrine, quote));
            }
        }

        if (any) return;

        var empty = new Label
        {
            Text = L10n.T("No other shrine found yet. They light up when you walk into them."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        empty.AddThemeFontSizeOverride("font_size", 12);
        empty.AddThemeColorOverride("font_color", Dim);
        _destinations.AddChild(empty);
    }

    /// <summary>A map's line in the list: its name, and the levels it is meant for.</summary>
    private static Control MapHeading(Zone zone)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var name = new Label { Text = GameItems.Localise(zone.Name) };
        name.AddThemeFontSizeOverride("font_size", 14);
        name.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.90f));
        row.AddChild(name);

        var band = new Label
        {
            Text = L10n.F("level {0}", zone.Band),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        band.AddThemeFontSizeOverride("font_size", 12);
        band.AddThemeColorOverride("font_color", Dim);
        row.AddChild(band);

        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 2);
        block.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        block.AddChild(row);

        return block;
    }

    /// <summary>
    /// One shrine: the lit-crystal mark, its name, and what the trip costs with the coin
    /// before it — red when the purse is short. The shrine underfoot says so instead.
    /// </summary>
    private Control Destination(Shrine shrine, TravelQuote quote)
    {
        var here = quote.Refusal == TravelRefusal.AlreadyHere;

        var button = new Button
        {
            CustomMinimumSize = new Vector2(0, 30),
            Disabled = !quote.Allowed,
            FocusMode = Control.FocusModeEnum.None,
        };

        var content = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        content.OffsetLeft = 10;
        content.OffsetRight = -10;
        content.AddThemeConstantOverride("separation", 8);
        button.AddChild(content);

        var mark = new Label { Text = "◆", VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        mark.AddThemeFontSizeOverride("font_size", 12);
        mark.AddThemeColorOverride("font_color", here ? Dim : ShrineGold);
        content.AddChild(mark);

        var name = new Label
        {
            Text = GameItems.Localise(shrine.Name),
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        name.AddThemeFontSizeOverride("font_size", 13);
        name.AddThemeColorOverride("font_color", here ? Dim : new Color(0.90f, 0.90f, 0.86f));
        content.AddChild(name);

        if (here)
        {
            var label = new Label { Text = L10n.T("you are here"), VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", Dim);
            content.AddChild(label);
        }
        else
        {
            var cost = Coin.Amount(quote.Cost, 13, quote.Refusal == TravelRefusal.NotEnoughYang ? Short : ShrineGold);
            cost.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            content.AddChild(cost);
        }

        // A refused trip still shows what it would cost, only dimmer.
        if (!quote.Allowed && !here) content.Modulate = new Color(1, 1, 1, 0.6f);

        var id = shrine.Id;
        button.Pressed += () => Travel(id);

        return button;
    }

    private void RefreshRespec()
    {
        var progression = _character?.Progression;

        if (progression is null)
        {
            _respec.Disabled = true;
            _respecCost.Text = "";
            return;
        }

        _respecCost.Text = L10n.F("Free. Returns every attribute point you have placed, including the opening spread the game assigned for you. Level {0}, {1} point(s) placed.",
            progression.Level, progression.Assigned.Total);

        _respec.Disabled = progression.Assigned.Total == 0;
    }

    private bool InCombat()
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is Combat.EnemyBrain { IsDead: false, HasLivingTarget: true }) return true;
        }

        return false;
    }

    // ------------------------------------------------------------------ actions

    private void Travel(string shrineId)
    {
        if (_inventory is null) return;

        var quote = GameWorld.Travel.Quote(shrineId, _inventory.Bag.Yang, InCombat());

        if (!quote.Allowed)
        {
            _status.Text = Explain(quote.Refusal);
            return;
        }

        var target = GetTree().GetNodesInGroup("shrines")
            .OfType<ShrineNode>()
            .FirstOrDefault(s => s.ShrineId == shrineId);

        // On another map: that map is loaded, and the player arrives at the shrine.
        if (target is null)
        {
            if (GameWorld.Graph.Shrine(shrineId) is not { } far)
            {
                _status.Text = Explain(TravelRefusal.UnknownShrine);
                return;
            }

            _inventory.Bag.TrySpendYang(quote.Cost);
            GameWorld.Travel.Discover(shrineId);
            _status.Text = "";
            Close();

            _character?.CarryOut();
            ZoneTransition.BeginToShrine(GetTree(), GameWorld.CurrentZoneId, far.Zone, shrineId);
            return;
        }

        _inventory.Bag.TrySpendYang(quote.Cost);

        if (GetTree().GetFirstNodeInGroup("player") is Node3D player)
        {
            player.GlobalPosition = target.GlobalPosition + ShrineNode.ArrivalOffset;
        }

        GD.Print($"[shrine] travel within {GameWorld.CurrentZoneId}, to {shrineId}");
        GameWorld.Travel.Discover(shrineId);
        _status.Text = "";
        Close();
    }

    private static string Explain(TravelRefusal refusal) => refusal switch
    {
        TravelRefusal.InCombat => L10n.T("Not while something is chasing you."),
        TravelRefusal.NotEnoughYang => L10n.T("Not enough gan."),
        TravelRefusal.NotDiscovered => L10n.T("You have not stood at that shrine yet."),
        TravelRefusal.NotATravelPoint => L10n.T("That is a checkpoint, not a shrine."),
        TravelRefusal.AlreadyHere => L10n.T("You are already here."),
        _ => L10n.T("You cannot travel there."),
    };

    /// <summary>
    /// Refunds every point the player has placed: attributes, and since REF-03 skills too.
    /// </summary>
    /// <remarks>
    /// Free, per FR-2.6 and the reasoning already recorded on
    /// <see cref="Kiln.Core.Progression.CharacterProgression.Respec"/>: in an MMO a ruined
    /// build is fixed by rolling another character, but offline it is a ruined save, so a
    /// price on respec only taxes the experimenting the build system exists to invite.
    /// <para>
    /// Skills come back with them now that points buy skills rather than levels handing them
    /// out: a build that went seven points deep into Whirlwind is exactly the kind of decision
    /// a player should be able to take back, and it is the same decision as an attribute
    /// spread. The character walks away from the shrine with no skills at all until the points
    /// are placed again, which is why it is worded plainly before it happens.
    /// </para>
    /// </remarks>
    private void DoRespec()
    {
        if (_character is null) return;

        var attributes = _character.Progression.Assigned.Total;
        var skills = _character.Skills.Refund();

        if (attributes == 0 && skills == 0) return;

        _character.Progression.Respec(skills);
        _character.RefreshStats();

        _status.Text = L10n.F("Returned {0} attribute point(s) and {1} skill point(s). Place them from the character sheet (C) and the skill screen (K).",
            attributes, skills);

        RefreshRespec();
    }

    // ------------------------------------------------------------------ chrome

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

    private static StyleBoxFlat Panel() => new()
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
    };
}
