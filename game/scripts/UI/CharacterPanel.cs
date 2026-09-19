using System.Collections.Generic;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The character sheet, on C (PRG-10): where attribute points are actually spent.
/// </summary>
/// <remarks>
/// This closes a loop that had been open since Phase 3. Levelling awarded four attribute
/// points every level and nothing in the game could spend them, so every point earned after
/// the starting allocation sat in a counter doing nothing — invisible, because an unspent
/// point looks exactly like a stat that has not grown yet.
/// <para>
/// Derived stats are listed beside the attributes and update on the same frame a point is
/// spent. Without that the player is spending a currency on faith: "+1 DEX" means nothing
/// until you can watch crit chance move, and watching it stop moving at the cap is how the
/// caps teach themselves.
/// </para>
/// <para>
/// Skills are shown but not spent here — see the note on <see cref="BuildSkills"/>.
/// </para>
/// </remarks>
public partial class CharacterPanel : CanvasLayer
{
    private sealed record Row(Label Value, Button Plus);

    private readonly Dictionary<AttributeKind, Row> _rows = [];

    private Player.PlayerCharacter? _character;
    private Combat.Combatant? _combatant;
    private Items.PlayerInventory? _inventory;

    private Label _title = null!;
    private Label _experience = null!;
    private Label _unspent = null!;
    private VBoxContainer _derived = null!;
    private VBoxContainer _skills = null!;
    private Label _hint = null!;
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
            Visible = !Visible;
            UiState.SetOpen(ref _counted, Visible);

            if (Visible) Refresh();

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

    // A panel freed while open would leave the modal count raised and the player unable to move.
    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

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

        root.AddThemeStyleboxOverride("panel", Panel());
        AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(margin);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 28);
        margin.AddChild(columns);

        BuildAttributes(columns);
        BuildDerived(columns);
        BuildSkills(columns);
    }

    private void BuildAttributes(HBoxContainer columns)
    {
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(310, 380) };
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);

        _title = Heading("Character");
        left.AddChild(_title);

        _experience = new Label();
        _experience.AddThemeFontSizeOverride("font_size", 13);
        _experience.AddThemeColorOverride("font_color", new Color(0.70f, 0.76f, 0.84f));
        left.AddChild(_experience);

        left.AddChild(new HSeparator());

        _unspent = new Label();
        _unspent.AddThemeFontSizeOverride("font_size", 15);
        _unspent.AddThemeColorOverride("font_color", new Color("8fd3a8"));
        left.AddChild(_unspent);

        foreach (var (kind, blurb) in Blurbs)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            left.AddChild(row);

            var name = new Label { Text = kind.ToString().ToUpperInvariant(), CustomMinimumSize = new Vector2(44, 0) };
            name.AddThemeFontSizeOverride("font_size", 15);
            row.AddChild(name);

            var value = new Label { CustomMinimumSize = new Vector2(34, 0), HorizontalAlignment = HorizontalAlignment.Right };
            value.AddThemeFontSizeOverride("font_size", 15);
            row.AddChild(value);

            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(30, 0) };
            var spent = kind;
            plus.Pressed += () => Spend(spent);
            row.AddChild(plus);

            var gain = new Label { Text = blurb, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            gain.AddThemeFontSizeOverride("font_size", 11);
            gain.AddThemeColorOverride("font_color", new Color(0.58f, 0.63f, 0.70f));
            gain.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(gain);

            _rows[kind] = new Row(value, plus);
        }

        _hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hint.AddThemeFontSizeOverride("font_size", 11);
        _hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        _hint.Text = "Points are yours to place however you like. The caps do the balancing: "
            + "crit stops at 50%, evasion at 30%, mitigation at 75%.\n\n"
            + "Your opening spread was assigned for you. Any shrine will refund it, free.";
        left.AddChild(_hint);
    }

    private void BuildDerived(HBoxContainer columns)
    {
        var middle = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
        middle.AddThemeConstantOverride("separation", 4);
        columns.AddChild(middle);

        middle.AddChild(Heading2("What it buys"));

        _derived = new VBoxContainer();
        _derived.AddThemeConstantOverride("separation", 3);
        middle.AddChild(_derived);
    }

    /// <summary>
    /// The skill list, read-only.
    /// </summary>
    /// <remarks>
    /// Skills unlock themselves at their level, and the sheet does not offer to spend a point
    /// on them, because there is no choice there to offer. The Warrior has eight skills and
    /// the curve grants one point per level, so by the time the last one unlocks at 24 the
    /// player is holding roughly three times the points the tree can absorb. A screen asking
    /// you to click "unlock" on the one thing you can afford, with points to spare, is a
    /// chore wearing the costume of a decision.
    /// <para>
    /// Making that a real choice means giving points a second sink — ranks bought rather than
    /// earned by use, or a tree where the branches genuinely compete. That is a design
    /// question, not a UI one, and it is flagged rather than invented here.
    /// </para>
    /// </remarks>
    private void BuildSkills(HBoxContainer columns)
    {
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        right.AddThemeConstantOverride("separation", 4);
        columns.AddChild(right);

        right.AddChild(Heading2("Skills"));

        _skills = new VBoxContainer();
        _skills.AddThemeConstantOverride("separation", 4);
        right.AddChild(_skills);

        var note = new Label
        {
            Text = "Skills unlock on their own at the level shown. Mastery comes from using them, not from points.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        note.AddThemeFontSizeOverride("font_size", 11);
        note.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        right.AddChild(note);
    }

    // ------------------------------------------------------------------ refresh

    private static readonly (AttributeKind Kind, string Blurb)[] Blurbs =
    [
        (AttributeKind.Str, "attack power"),
        (AttributeKind.Dex, "crit, pierce, evasion, attack speed"),
        (AttributeKind.Int, "mana pool and regeneration"),
        (AttributeKind.Vit, "health, defence, regeneration"),
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

        _title.Text = $"Character — level {progression.Level}";
        _experience.Text = progression.IsMaxLevel
            ? "Maximum level."
            : $"{progression.Experience:N0} / {progression.ExperienceForNextLevel:N0} xp to level {progression.Level + 1}";

        _unspent.Text = points > 0
            ? $"{points} attribute point{(points == 1 ? "" : "s")} to place"
            : "No attribute points to place.";

        _unspent.AddThemeColorOverride("font_color",
            points > 0 ? new Color("8fd3a8") : new Color(0.55f, 0.60f, 0.66f));

        foreach (var (kind, _) in Blurbs)
        {
            var row = _rows[kind];

            row.Value.Text = Value(attributes, kind).ToString();
            row.Plus.Disabled = points <= 0;
        }

        RefreshDerived();
        RefreshSkills();
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

        Stat("Health", $"{s.MaxHp:N0}");
        Stat("Mana", $"{s.MaxMana:N0}");
        Stat("Attack power", $"{s.AttackPower:N0}");
        Stat("Defence", $"{s.Defense:N0}");
        Stat("Attacks / sec", $"{s.AttacksPerSecond:F2}");

        // The capped ones say so once they are there, so the ceiling is discovered by
        // reading rather than by wasting ten points finding it.
        Capped("Crit chance", s.CritChance, StatBlock.CritChanceCap);
        Capped("Pierce", s.PierceChance, StatBlock.PierceChanceCap);
        Capped("Evasion", s.Evasion, StatBlock.EvasionCap);

        Stat("Health regen", $"{s.HpRegenPerSecond:F1} / s");
        Stat("Mana regen", $"{s.ManaRegenPerSecond:F1} / s");
    }

    private void Stat(string name, string value)
    {
        var row = new HBoxContainer();

        var label = new Label { Text = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.70f, 0.75f, 0.82f));
        row.AddChild(label);

        var amount = new Label { Text = value };
        amount.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(amount);

        _derived.AddChild(row);
    }

    private void Capped(string name, double value, double cap)
    {
        var atCap = value >= cap - 0.0001;

        Stat(name, atCap ? $"{value:P0}  (cap)" : $"{value:P1}");

        if (!atCap) return;

        if (_derived.GetChild(_derived.GetChildCount() - 1) is HBoxContainer row
            && row.GetChild(1) is Label amount)
        {
            amount.AddThemeColorOverride("font_color", new Color("e0b356"));
        }
    }

    private void RefreshSkills()
    {
        foreach (var child in _skills.GetChildren()) child.QueueFree();

        if (_character is null || !GameContent.IsLoaded) return;

        var book = _character.Skills;

        foreach (var def in GameContent.Database.Skills.Values)
        {
            if (def.Class != CharacterClass.Warrior) continue;

            var known = book.IsUnlocked(def.Id);
            var text = known
                ? $"{GameItems.Localise(def.Name)}  ·  {book.RankOf(def.Id)}"
                : $"{GameItems.Localise(def.Name)}  ·  level {def.UnlockLevel}";

            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color",
                known ? new Color(0.88f, 0.90f, 0.94f) : new Color(0.45f, 0.49f, 0.55f));
            _skills.AddChild(label);

            if (!known) continue;

            var toNext = book.UsesToNextRank(def.Id);

            if (toNext <= 0) continue;

            var progress = new Label { Text = $"      {toNext} more uses to rank up" };
            progress.AddThemeFontSizeOverride("font_size", 11);
            progress.AddThemeColorOverride("font_color", new Color(0.50f, 0.56f, 0.62f));
            _skills.AddChild(progress);
        }
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
