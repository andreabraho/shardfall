using System.Linq;
using Godot;
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

    /// <summary>
    /// Flat, and modest. A respec priced to hurt does not make the first choice more
    /// meaningful; it makes the player look the answer up instead of finding it.
    /// </summary>
    public const long RespecYang = 600;

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
            ? $"{label} — shrine discovered.  [F] to travel or respec"
            : $"{label} — rested.  [F] to travel or respec";

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

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        _title = Heading("Shrine");
        column.AddChild(_title);

        _notice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _notice.AddThemeFontSizeOverride("font_size", 12);
        _notice.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.64f));
        column.AddChild(_notice);

        column.AddChild(new HSeparator());
        column.AddChild(Heading2("Travel"));

        _destinations = new VBoxContainer();
        _destinations.AddThemeConstantOverride("separation", 4);
        column.AddChild(_destinations);

        column.AddChild(new HSeparator());
        column.AddChild(Heading2("Reconsider"));

        _respecCost = new Label();
        _respecCost.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_respecCost);

        _respec = new Button { Text = "Refund attribute and skill points" };
        _respec.Pressed += DoRespec;
        column.AddChild(_respec);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_status);

        var close = new Button { Text = "Close  (Esc)" };
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
            : $"{GameItems.Localise(zone.Name)} · level {zone.Band} · "
              + $"{GameWorld.Travel.Discovered.Count} shrine(s) found";

        RefreshTravel();
        RefreshRespec();
    }

    private void RefreshTravel()
    {
        foreach (var child in _destinations.GetChildren()) child.QueueFree();

        var yang = _inventory?.Bag.Yang ?? 0;
        var inCombat = InCombat();
        var any = false;

        foreach (var shrine in GameWorld.Travel.Destinations())
        {
            var quote = GameWorld.Travel.Quote(shrine.Id, yang, inCombat);

            // Where you are standing is not a destination; everything else is listed even when
            // it is refused, so the player can see what the trip would cost.
            if (quote.Refusal == TravelRefusal.AlreadyHere) continue;

            any = true;

            var zone = GameWorld.Graph[shrine.Zone];
            var row = new Button
            {
                Text = $"{GameItems.Localise(shrine.Name)}  —  {GameItems.Localise(zone?.Name ?? "")}"
                    + $"   {quote.Cost:N0} yang",
                Disabled = !quote.Allowed,
                Alignment = HorizontalAlignment.Left,
            };

            var id = shrine.Id;
            row.Pressed += () => Travel(id);
            _destinations.AddChild(row);
        }

        if (any) return;

        var empty = new Label
        {
            Text = "No other shrine found yet. They light up when you walk into them.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        empty.AddThemeFontSizeOverride("font_size", 12);
        empty.AddThemeColorOverride("font_color", new Color(0.55f, 0.58f, 0.62f));
        _destinations.AddChild(empty);
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

        var yang = _inventory?.Bag.Yang ?? 0;

        _respecCost.Text = $"{RespecYang:N0} yang — returns every attribute point and every "
            + $"skill point spent. Level {progression.Level}.";

        _respec.Disabled = yang < RespecYang;
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

        if (target is null)
        {
            // The destination is in another zone's scene. Loading that scene is WLD-01's
            // remaining half; refusing is honest, and silently charging for nothing is not.
            _status.Text = "That shrine is in another zone — zone loading is not wired up yet.";
            return;
        }

        _inventory.Bag.TrySpendYang(quote.Cost);

        if (GetTree().GetFirstNodeInGroup("player") is Node3D player)
        {
            player.GlobalPosition = target.GlobalPosition + new Vector3(0, 0.1f, 2.5f);
        }

        GameWorld.Travel.Discover(shrineId);
        _status.Text = "";
        Close();
    }

    private static string Explain(TravelRefusal refusal) => refusal switch
    {
        TravelRefusal.InCombat => "Not while something is chasing you.",
        TravelRefusal.NotEnoughYang => "Not enough yang.",
        TravelRefusal.NotDiscovered => "You have not stood at that shrine yet.",
        TravelRefusal.NotATravelPoint => "That is a checkpoint, not a shrine.",
        TravelRefusal.AlreadyHere => "You are already here.",
        _ => "You cannot travel there.",
    };

    private void DoRespec()
    {
        if (_inventory is null || _character is null) return;
        if (!_inventory.Bag.TrySpendYang(RespecYang))
        {
            _status.Text = "Not enough yang.";
            return;
        }

        var book = _character.Skills;
        var spent = book.Count;

        _character.Progression.Respec(spent);
        book.Clear();
        _inventory.ApplyToStats();

        _status.Text = $"Refunded every attribute point and {spent} skill point(s). "
            + "Spend them again from the character screen.";

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
