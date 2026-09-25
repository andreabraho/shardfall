using System.Collections.Generic;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Game.Input;

namespace Kiln.Game.UI;

/// <summary>
/// The character sheet, on C (PRG-10; REF-19 in the original's style): who the character is,
/// where attribute points are spent, and what they buy.
/// </summary>
/// <remarks>
/// The original's character window: a framed column on the left of the screen. At the top the
/// name, the class, the level and the experience bar; then the four attributes, each with its
/// "+", and the points left to place; then the statistics they buy, updated on the frame a
/// point is spent — "+1 DEX" means nothing until you can watch crit chance move, and watching
/// it stop at the cap is how the caps teach themselves.
/// <para>
/// Skills have their own window (K) and are no longer repeated here.
/// </para>
/// </remarks>
public partial class CharacterPanel : CanvasLayer
{
    private sealed record Row(Label Value, Button Plus);

    private const float Width = 340f;

    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Frame = new(0.62f, 0.50f, 0.28f);
    private static readonly Color Soft = new(0.72f, 0.70f, 0.64f);
    private static readonly Color Dim = new(0.55f, 0.53f, 0.48f);

    private readonly Dictionary<AttributeKind, Row> _rows = [];

    private Player.PlayerCharacter? _character;
    private Combat.Combatant? _combatant;
    private Items.PlayerInventory? _inventory;

    private Label _name = null!;
    private Label _level = null!;
    private ProgressBar _expBar = null!;
    private Label _expText = null!;
    private Label _unspent = null!;
    private GridContainer _derived = null!;
    private bool _counted;

    public override void _Ready()
    {
        Layer = 23;
        Build();
        Visible = false;
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        var player = GetTree().GetFirstNodeInGroup("player");

        _character = player?.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter");
        _combatant = player?.GetNodeOrNull<Combat.Combatant>("Combatant");
        _inventory = player?.GetNodeOrNull<Items.PlayerInventory>("PlayerInventory");

        if (_character is not null)
        {
            _character.LeveledUp += _ => Refresh();
            _character.ExperienceChanged += Refresh;
        }

        if (_inventory is not null) _inventory.Changed += Refresh;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.ToggleCharacter))
        {
            SetOpen(!Visible);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed(GameActions.Cancel))
        {
            SetOpen(false);
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetOpen(bool open)
    {
        Visible = open;
        UiState.SetOpen(ref _counted, open);

        if (open) Refresh();
    }

    // A panel freed while open would leave the modal count raised and the player unable to move.
    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var root = new PanelContainer
        {
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = 24,
            OffsetRight = 24 + Width,
            OffsetTop = 210,
            GrowVertical = Control.GrowDirection.End,
        };

        // Moved by holding its border or title and dragging (REF-19).
        PanelMover.Attach(root, "character");

        root.AddThemeStyleboxOverride("panel", Box(new Color(0.06f, 0.055f, 0.05f, 0.96f), 2, 0));
        AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        root.AddChild(column);

        // Title bar.
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), 0, 8, bottom: 1));
        column.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        var title = new Label { Text = L10n.T("Character"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", Gold);
        barRow.AddChild(title);

        var cross = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None };
        cross.AddThemeFontSizeOverride("font_size", 13);
        cross.Pressed += () => SetOpen(false);
        barRow.AddChild(cross);

        var body = new MarginContainer();
        body.AddThemeConstantOverride("margin_left", 14);
        body.AddThemeConstantOverride("margin_right", 14);
        body.AddThemeConstantOverride("margin_top", 10);
        body.AddThemeConstantOverride("margin_bottom", 12);
        column.AddChild(body);

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 6);
        body.AddChild(inner);

        BuildIdentity(inner);
        BuildAttributes(inner);
        BuildDerived(inner);

        var note = new Label
        {
            Text = L10n.T("The caps do the balancing: crit stops at 50%, evasion at 30%, mitigation at 75%. Any shrine refunds your points, free."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(Width - 28, 0),
        };

        note.AddThemeFontSizeOverride("font_size", 11);
        note.AddThemeColorOverride("font_color", Dim);
        inner.AddChild(note);
    }

    /// <summary>The name, the class and level, and the experience bar.</summary>
    private void BuildIdentity(VBoxContainer inner)
    {
        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Box(new Color(0.10f, 0.09f, 0.08f), 1, 6, edge: new Color(0.30f, 0.25f, 0.18f)));
        inner.AddChild(plate);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        plate.AddChild(box);

        var top = new HBoxContainer();
        box.AddChild(top);

        _name = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _name.AddThemeFontSizeOverride("font_size", 16);
        _name.AddThemeColorOverride("font_color", Gold);
        top.AddChild(_name);

        _level = new Label();
        _level.AddThemeFontSizeOverride("font_size", 13);
        _level.AddThemeColorOverride("font_color", Soft);
        top.AddChild(_level);

        var holder = new Control { CustomMinimumSize = new Vector2(0, 16) };
        box.AddChild(holder);

        _expBar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _expBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _expBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.85f, 0.66f, 0.22f) });
        _expBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.035f, 0.03f),
            BorderColor = new Color(0.45f, 0.37f, 0.22f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });

        holder.AddChild(_expBar);

        _expText = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        _expText.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _expText.AddThemeFontSizeOverride("font_size", 11);
        _expText.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _expText.AddThemeConstantOverride("outline_size", 3);
        holder.AddChild(_expText);
    }

    private void BuildAttributes(VBoxContainer inner)
    {
        inner.AddChild(Heading(L10n.T("Attributes")));

        _unspent = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _unspent.AddThemeFontSizeOverride("font_size", 13);
        inner.AddChild(_unspent);

        foreach (var (kind, blurb) in Blurbs)
        {
            var frame = new PanelContainer { TooltipText = blurb };
            frame.AddThemeStyleboxOverride("panel", Box(new Color(0.10f, 0.09f, 0.08f), 1, 4, edge: new Color(0.30f, 0.25f, 0.18f)));
            inner.AddChild(frame);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            frame.AddChild(row);

            // The attribute's short name on a small gold plate, as the original sets it.
            var tag = new PanelContainer { CustomMinimumSize = new Vector2(46, 26) };
            tag.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), 1, 0, edge: Frame));
            row.AddChild(tag);

            var name = new Label { Text = Words.Of(kind), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            name.AddThemeFontSizeOverride("font_size", 13);
            name.AddThemeColorOverride("font_color", Gold);
            tag.AddChild(name);

            var value = new Label { CustomMinimumSize = new Vector2(34, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            value.AddThemeFontSizeOverride("font_size", 15);
            row.AddChild(value);

            var gain = new Label
            {
                Text = blurb,
                ClipText = true,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };

            gain.AddThemeFontSizeOverride("font_size", 11);
            gain.AddThemeColorOverride("font_color", Dim);
            row.AddChild(gain);

            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(28, 26), FocusMode = Control.FocusModeEnum.None };
            plus.AddThemeFontSizeOverride("font_size", 15);

            var spent = kind;
            plus.Pressed += () => Spend(spent);
            row.AddChild(plus);

            _rows[kind] = new Row(value, plus);
        }
    }

    private void BuildDerived(VBoxContainer inner)
    {
        inner.AddChild(Heading(L10n.T("Statistics")));

        _derived = new GridContainer { Columns = 2 };
        _derived.AddThemeConstantOverride("h_separation", 12);
        _derived.AddThemeConstantOverride("v_separation", 2);
        inner.AddChild(_derived);
    }

    // ------------------------------------------------------------------ refresh

    /// <remarks>A property, so the words are read in whatever language is current.</remarks>
    private static (AttributeKind Kind, string Blurb)[] Blurbs =>
    [
        (AttributeKind.Str, L10n.T("attack power")),
        (AttributeKind.Dex, L10n.T("crit, pierce, evasion, attack speed")),
        (AttributeKind.Int, L10n.T("mana pool and regeneration")),
        (AttributeKind.Vit, L10n.T("health, defence, regeneration")),
    ];

    private void Spend(AttributeKind kind)
    {
        if (_character?.SpendAttribute(kind) != true) return;

        Refresh();
    }

    private void Refresh()
    {
        if (_character is null || !Visible) return;

        var progression = _character.Progression;
        var attributes = progression.TotalAttributes;
        var points = progression.UnspentAttributePoints;

        _name.Text = Saving.SaveService.CharacterName is { Length: > 0 } given ? given : Saving.SaveService.DefaultName();
        _level.Text = L10n.F("{0}  ·  level {1}", L10n.T("Warrior"), progression.Level);

        _expBar.Value = progression.IsMaxLevel ? 1 : progression.LevelProgress;
        _expText.Text = progression.IsMaxLevel
            ? L10n.T("Maximum level.")
            : L10n.F("{0:N0} / {1:N0}  ({2:0.0}%)", progression.Experience, progression.ExperienceForNextLevel, progression.LevelProgress * 100);

        _unspent.Text = L10n.F("Points to place: {0}", points);
        _unspent.AddThemeColorOverride("font_color", points > 0 ? new Color(0.56f, 0.86f, 0.62f) : Dim);

        foreach (var (kind, _) in Blurbs)
        {
            var row = _rows[kind];

            row.Value.Text = Value(attributes, kind).ToString();
            row.Plus.Disabled = points <= 0;
            row.Plus.Visible = points > 0;
        }

        RefreshDerived();
    }

    private static int Value(Attributes attributes, AttributeKind kind) => kind switch
    {
        AttributeKind.Str => attributes.Str,
        AttributeKind.Dex => attributes.Dex,
        AttributeKind.Int => attributes.Int,
        _ => attributes.Vit,
    };

    private void RefreshDerived()
    {
        foreach (var child in _derived.GetChildren()) child.QueueFree();

        if (_combatant is null) return;

        var s = _combatant.Stats;

        Stat(L10n.T("Health"), L10n.F("{0:N0}", s.MaxHp));
        Stat(L10n.T("Mana"), L10n.F("{0:N0}", s.MaxMana));
        Stat(L10n.T("Attack power"), L10n.F("{0:N0}", s.AttackPower));
        Stat(L10n.T("Defence"), L10n.F("{0:N0}", s.Defense));
        Stat(L10n.T("Attacks / sec"), L10n.F("{0:F2}", s.AttacksPerSecond));

        // The capped ones say so once they are there, so the ceiling is discovered by
        // reading rather than by wasting ten points finding it.
        Capped(L10n.T("Crit chance"), s.CritChance, StatBlock.CritChanceCap);
        Capped(L10n.T("Pierce"), s.PierceChance, StatBlock.PierceChanceCap);
        Capped(L10n.T("Evasion"), s.Evasion, StatBlock.EvasionCap);

        Stat(L10n.T("Health regen"), L10n.F("{0:F1} / s", s.HpRegenPerSecond));
        Stat(L10n.T("Mana regen"), L10n.F("{0:F1} / s", s.ManaRegenPerSecond));
    }

    private void Stat(string name, string value, Color? colour = null)
    {
        var label = new Label { Text = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Soft);
        _derived.AddChild(label);

        var amount = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(90, 0) };
        amount.AddThemeFontSizeOverride("font_size", 12);
        amount.AddThemeColorOverride("font_color", colour ?? new Color(0.94f, 0.92f, 0.86f));
        _derived.AddChild(amount);
    }

    private void Capped(string name, double value, double cap)
    {
        var atCap = value >= cap - 0.0001;

        Stat(name, atCap ? L10n.F("{0:P0}  (cap)", value) : L10n.F("{0:P1}", value),
            atCap ? new Color("e0b356") : null);
    }

    // ------------------------------------------------------------------ chrome

    private static Control Heading(string text)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.85f, 0.72f, 0.45f));
        box.AddChild(label);

        box.AddChild(new ColorRect { Color = new Color(Frame, 0.6f), CustomMinimumSize = new Vector2(0, 1) });

        return box;
    }

    private static StyleBoxFlat Box(Color fill, int width, int margin, int bottom = -1, Color? edge = null) => new()
    {
        BgColor = fill,
        BorderColor = edge ?? Frame,
        BorderWidthTop = width,
        BorderWidthLeft = width,
        BorderWidthRight = width,
        BorderWidthBottom = bottom >= 0 ? bottom : width,
        ContentMarginLeft = margin + 4,
        ContentMarginRight = margin,
        ContentMarginTop = margin * 0.6f,
        ContentMarginBottom = margin * 0.6f,
    };
}
