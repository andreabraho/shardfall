using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Input;

namespace Kiln.Game.UI;

/// <summary>
/// Every statistic the character has now, by kind (2026-10-09, at your call): attack, defence,
/// what it is strong against, what it resists, its state and the effects on it. Opened by the
/// arrow on the right edge of the character window, and kept beside it.
/// </summary>
/// <remarks>
/// The character window shows what attribute points buy; this shows everything, gear and
/// blessings included, so a bracelet's "+10% against the undead" can be seen adding up. Read
/// again a few times a second while open: a blessing running out shows as it happens.
/// </remarks>
public partial class StatsPanel : CanvasLayer
{
    private const float Width = 300f;

    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Frame = new(0.62f, 0.50f, 0.28f);
    private static readonly Color Soft = new(0.72f, 0.70f, 0.64f);
    private static readonly Color Dim = new(0.55f, 0.53f, 0.48f);
    private static readonly Color Bright = new(0.94f, 0.92f, 0.86f);
    private static readonly Color Good = new(0.56f, 0.86f, 0.62f);
    private static readonly Color Bad = new(0.92f, 0.45f, 0.40f);
    private static readonly Color AtCap = new("e0b356");

    private readonly List<(HBoxContainer Line, Label Name, Label Amount)> _rows = [];
    private readonly List<Control> _headings = [];

    private Combat.Combatant? _combatant;
    private Control _root = null!;
    private VBoxContainer _list = null!;
    private bool _counted;
    private double _since;
    private int _row;
    private int _heading;

    /// <summary>Where the window it belongs to stands, read every frame so it follows a drag.</summary>
    public System.Func<Rect2>? Beside { get; set; }

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 23;
        Build();
        Visible = false;
        CallDeferred(nameof(Bind));
    }

    private void Bind() =>
        _combatant = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<Combat.Combatant>("Combatant");

    public override void _ExitTree() => UiState.SetSide(ref _counted, _root, false);

    public void SetOpen(bool open)
    {
        Visible = open;

        // A side panel, as the character window it opens from: play goes on.
        UiState.SetSide(ref _counted, _root, open);

        if (open)
        {
            Follow();
            Refresh();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed(GameActions.Cancel)) return;

        SetOpen(false);
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        Follow();

        _since += delta;
        if (_since < 0.25) return;

        _since = 0;
        Refresh();
    }

    private void Follow()
    {
        if (Beside?.Invoke() is not { } rect) return;

        _root.Position = new Vector2(rect.End.X + 30, rect.Position.Y);
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var root = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0) };
        _root = root;

        root.AddThemeStyleboxOverride("panel", Box(new Color(0.06f, 0.055f, 0.05f, 0.96f), 2, 0));
        AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        root.AddChild(column);

        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), 0, 8, bottom: 1));
        column.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        var title = new Label { Text = L10n.T("Statistics"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
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
        body.AddThemeConstantOverride("margin_top", 8);
        body.AddThemeConstantOverride("margin_bottom", 12);
        column.AddChild(body);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 2);
        body.AddChild(_list);
    }

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        _row = 0;
        _heading = 0;

        if (_combatant is null) return;

        var s = _combatant.Stats;
        var m = s.Modifiers;
        var statuses = _combatant.Statuses;
        var eye = statuses.Has(StatusKind.Focus);

        Heading(L10n.T("Attack"));
        Row(L10n.T("Attack power"), L10n.F("{0:N0}", s.AttackPower + statuses.MagnitudeOf(StatusKind.Might)));
        if (PlayerProfile.Class is CharacterClass.Shaman or CharacterClass.Sura) Row(L10n.T("Magic power"), L10n.F("{0:N0}", s.MagicPower));
        Row(L10n.T("Attacks / sec"), L10n.F("{0:F2}", s.AttacksPerSecond));

        // The crit as it rolls: the Dragon's Eye's share on top, and no ceiling while it is open.
        var crit = eye
            ? System.Math.Min(1.0, s.UncappedCritChance + statuses.MagnitudeOf(StatusKind.Focus))
            : System.Math.Min(StatBlock.CritChanceCap, s.CritChance);
        if (eye) Row(L10n.T("Crit chance"), L10n.F("{0:P1}  (no cap)", crit), Good);
        else Capped(L10n.T("Crit chance"), crit, StatBlock.CritChanceCap);

        Row(L10n.T("Crit damage"), L10n.F("×{0:F2}", s.CritDamage));
        Capped(L10n.T("Pierce"), s.PierceChance, StatBlock.PierceChanceCap);
        Bonus(L10n.T("Damage"), m.DamagePct);
        Bonus(L10n.T("Skill damage"), m.SkillDamagePct);

        Heading(L10n.T("Defence"));
        Row(L10n.T("Health"), L10n.F("{0:N0}", s.MaxHp));
        Row(L10n.T("Defence"), L10n.F("{0:N0}", s.Defense));
        Capped(L10n.T("Damage blocked"), s.DamageReductionAgainst(s.Level), StatBlock.MitigationCap);
        Capped(L10n.T("Evasion"), s.Evasion, StatBlock.EvasionCap);

        Heading(L10n.T("Strong against"));
        foreach (var family in System.Enum.GetValues<MonsterFamily>()) Bonus(Capital(BonusStat.FamilyName(family)), m.VsFamilyBonus(family));

        Heading(L10n.T("Resistances"));
        foreach (var (element, name) in Elements) Bonus(name, s.ResistTo(element));

        Heading(L10n.T("State"));
        Row(L10n.T("Mana"), L10n.F("{0:N0}", s.MaxMana));
        Row(L10n.T("Health regen"), L10n.F("{0:F1} / s", s.HpRegenPerSecond));
        Row(L10n.T("Mana regen"), L10n.F("{0:F1} / s", s.ManaRegenPerSecond));
        Bonus(L10n.T("Health on a kill"), m.KillRegenPct);
        Bonus(L10n.T("Movement speed"), m.MoveSpeedPct);

        Heading(L10n.T("Effects on you"));
        var active = statuses.Active.Where(e => e.Remaining > 0).OrderBy(e => e.Remaining).ToList();

        if (active.Count == 0) Row(L10n.T("None"), "", Dim);

        foreach (var effect in active)
        {
            var harmful = System.Array.IndexOf(StatusEffectSet.Harmful, effect.Kind) >= 0;
            Row(Words.Of(effect.Kind), L10n.F("{0:0} s", effect.Remaining), harmful ? Bad : Good);
        }

        // Rows and headings past what this refresh wrote are hidden, not freed: the list keeps
        // its controls, and only what changed is rewritten.
        for (var i = _row; i < _rows.Count; i++) _rows[i].Line.Visible = false;
        for (var i = _heading; i < _headings.Count; i++) _headings[i].Visible = false;

        Callable.From(() => { if (IsInstanceValid(_root)) _root.ResetSize(); }).CallDeferred();
    }

    /// <remarks>A property, so the words are read in whatever language is current.</remarks>
    private static (DamageElement Element, string Name)[] Elements =>
    [
        (DamageElement.Fire, L10n.T("Fire")),
        (DamageElement.Ice, L10n.T("Ice")),
        (DamageElement.Lightning, L10n.T("Lightning")),
        (DamageElement.Dark, L10n.T("Dark")),
    ];

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];

    private void Bonus(string name, double value) =>
        Row(name, value == 0 ? "—" : L10n.F("{0:+0.#%;-0.#%}", value), value == 0 ? Dim : value > 0 ? Good : Bad);

    private void Capped(string name, double value, double cap)
    {
        var atCap = value >= cap - 0.0001;

        Row(name, atCap ? L10n.F("{0:P0}  (cap)", value) : L10n.F("{0:P1}", value), atCap ? AtCap : null);
    }

    // ------------------------------------------------------------------ rows

    /// <summary>
    /// A section's title. Headings and rows are made once and reused in order; each is moved to
    /// the end of the list as it is written, so the list always reads in the order of this refresh.
    /// </summary>
    private void Heading(string text)
    {
        if (_heading >= _headings.Count)
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 2);

            // Air above every heading but the first.
            box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

            var label = new Label();
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.72f, 0.45f));
            box.AddChild(label);

            box.AddChild(new ColorRect { Color = new Color(Frame, 0.6f), CustomMinimumSize = new Vector2(0, 1) });

            _list.AddChild(box);
            _headings.Add(box);
        }

        var heading = _headings[_heading];

        heading.GetChild<Control>(0).Visible = _heading > 0;
        heading.GetChild<Label>(1).Text = text;
        heading.Visible = true;
        _list.MoveChild(heading, -1);
        _heading++;
    }

    private void Row(string name, string value, Color? colour = null)
    {
        if (_row >= _rows.Count)
        {
            var line = new HBoxContainer();

            var label = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", Soft);
            line.AddChild(label);

            var amount = new Label { HorizontalAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(110, 0) };
            amount.AddThemeFontSizeOverride("font_size", 12);
            line.AddChild(amount);

            _list.AddChild(line);
            _rows.Add((line, label, amount));
        }

        var (row, nameLabel, amountLabel) = _rows[_row++];

        row.Visible = true;
        _list.MoveChild(row, -1);
        nameLabel.Text = name;
        amountLabel.Text = value;
        amountLabel.AddThemeColorOverride("font_color", colour ?? Bright);
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
