using System;
using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Combat;
using Kiln.Game.Input;
using Kiln.Game.Player;

namespace Kiln.Game.UI;

/// <summary>
/// The bar along the bottom of the screen, in the original's layout (REF-19): health and mana
/// on the left with the level and the four experience orbs, the skills in the middle (the
/// <see cref="SkillBar"/> sits on it), the flask and the window buttons on the right.
/// </summary>
/// <remarks>
/// The buttons do exactly what their keys do — they send the key's action — so a panel opened
/// by a click and one opened by its key are the same panel in the same state.
/// </remarks>
public partial class TaskBar : CanvasLayer
{
    /// <summary>Height of the bar. The skill slots sit inside it; the message log above it.</summary>
    public const float Height = 64f;

    private static readonly Color Gold = new(0.78f, 0.64f, 0.36f);
    private static readonly Color Text = new(0.92f, 0.90f, 0.84f);

    private Combatant? _player;
    private PlayerCharacter? _character;
    private HealthFlask? _flask;

    private ProgressBar _health = null!;
    private ProgressBar _mana = null!;
    private Label _healthText = null!;
    private Label _manaText = null!;
    private Label _level = null!;
    private ExpOrbs _orbs = null!;
    private Label _expText = null!;
    private Label _statuses = null!;
    private Label _flaskCharges = null!;

    public override void _Ready()
    {
        Layer = 14;
        Build();
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        var body = GetTree().GetFirstNodeInGroup("player");

        _player = body?.GetNodeOrNull<Combatant>("Combatant");
        _character = body?.GetNodeOrNull<PlayerCharacter>("PlayerCharacter");
        _flask = body?.GetNodeOrNull<HealthFlask>("HealthFlask");

        if (_character is not null)
        {
            _character.ExperienceChanged += RefreshExperience;
            _character.LeveledUp += _ => RefreshExperience();
            RefreshExperience();
        }

        if (_flask is not null)
        {
            _flask.ChargesChanged += (_, _) => RefreshFlask();
            RefreshFlask();
        }
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var bar = new PanelContainer
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetTop = -Height,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        bar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.075f, 0.06f, 0.94f),
            BorderColor = Gold,
            BorderWidthTop = 2,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        });

        AddChild(bar);

        // A click on the bar — a button, a slot, the frame — is never a walk order.
        UiState.AddHud(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        bar.AddChild(row);

        row.AddChild(BuildVitals());

        // The middle belongs to the skill bar, which is drawn on its own layer over this one.
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        row.AddChild(BuildFlask());
        row.AddChild(BuildButtons());

        // Statuses float just over the bar's left end, where the eye already is for health.
        _statuses = new Label
        {
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = 18,
            OffsetTop = -Height - 24,
            OffsetBottom = -Height - 4,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        _statuses.AddThemeFontSizeOverride("font_size", 13);
        _statuses.AddThemeColorOverride("font_color", new Color(0.95f, 0.75f, 0.55f));
        _statuses.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _statuses.AddThemeConstantOverride("outline_size", 4);
        AddChild(_statuses);
    }

    /// <summary>The level, health and mana, and the orbs under them.</summary>
    private Control BuildVitals()
    {
        var block = new HBoxContainer();
        block.AddThemeConstantOverride("separation", 10);

        // The level in a gold medallion.
        var medal = new PanelContainer { CustomMinimumSize = new Vector2(46, 46), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        medal.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.12f, 0.07f),
            BorderColor = Gold,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 23,
            CornerRadiusTopRight = 23,
            CornerRadiusBottomLeft = 23,
            CornerRadiusBottomRight = 23,
            AntiAliasing = true,
        });

        _level = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _level.AddThemeFontSizeOverride("font_size", 16);
        _level.AddThemeColorOverride("font_color", new Color(0.96f, 0.86f, 0.58f));
        medal.AddChild(_level);
        medal.TooltipText = L10n.T("Level");
        block.AddChild(medal);

        var bars = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        bars.AddThemeConstantOverride("separation", 2);
        block.AddChild(bars);

        (_health, _healthText) = Bar(new Color("c0392b"), new Vector2(240, 15), 11);
        (_mana, _manaText) = Bar(new Color("3f7fd0"), new Vector2(240, 13), 10);

        bars.AddChild(_health.GetParent<Control>());
        bars.AddChild(_mana.GetParent<Control>());

        var exp = new HBoxContainer();
        exp.AddThemeConstantOverride("separation", 8);
        bars.AddChild(exp);

        _orbs = new ExpOrbs();
        exp.AddChild(_orbs);

        _expText = new Label { VerticalAlignment = VerticalAlignment.Center };
        _expText.AddThemeFontSizeOverride("font_size", 10);
        _expText.AddThemeColorOverride("font_color", new Color(0.85f, 0.76f, 0.52f));
        exp.AddChild(_expText);

        return block;
    }

    /// <summary>A bar with its numbers written across it, as the original does.</summary>
    private static (ProgressBar Bar, Label Text) Bar(Color fill, Vector2 size, int fontSize)
    {
        var holder = new Control { CustomMinimumSize = size };

        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill });
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.04f, 0.04f, 0.95f),
            BorderColor = new Color(0.45f, 0.37f, 0.22f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });

        holder.AddChild(bar);

        var text = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        text.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        text.AddThemeFontSizeOverride("font_size", fontSize);
        text.AddThemeColorOverride("font_color", Text);
        text.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        text.AddThemeConstantOverride("outline_size", 3);
        holder.AddChild(text);

        return (bar, text);
    }

    /// <summary>The flask, on Q: its icon and how many charges are left.</summary>
    private Control BuildFlask()
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(46, 46), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        slot.TooltipText = L10n.T("Health flask — heals and restores mana. Draughts to fill it are sold by merchants.");
        slot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.04f, 0.04f, 0.95f),
            BorderColor = Gold,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });

        var stack = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        slot.AddChild(stack);

        if (Items.GameItems.Spec(HealthFlask.DraughtId) is { } draught && ItemIcons.For(HealthFlask.DraughtId, draught) is { } icon)
        {
            var picture = new TextureRect
            {
                Texture = icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Modulate = new Color(0.95f, 0.45f, 0.40f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

            picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            picture.OffsetLeft = 6;
            picture.OffsetTop = 6;
            picture.OffsetRight = -6;
            picture.OffsetBottom = -6;
            stack.AddChild(picture);
        }

        var key = new Label { Text = GameActions.DescribeBinding(GameActions.HealthFlask), Position = new Vector2(3, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        key.AddThemeFontSizeOverride("font_size", 11);
        key.AddThemeColorOverride("font_color", new Color(0.85f, 0.80f, 0.70f));
        stack.AddChild(key);

        _flaskCharges = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        _flaskCharges.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _flaskCharges.OffsetRight = -4;
        _flaskCharges.OffsetBottom = -1;
        _flaskCharges.AddThemeFontSizeOverride("font_size", 12);
        _flaskCharges.AddThemeColorOverride("font_color", Text);
        _flaskCharges.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _flaskCharges.AddThemeConstantOverride("outline_size", 3);
        stack.AddChild(_flaskCharges);

        return slot;
    }

    /// <summary>One button for each window, as on the original's bar.</summary>
    private Control BuildButtons()
    {
        var grid = new GridContainer { Columns = 5, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);

        foreach (var (label, action, tip) in new[]
        {
            (L10n.T("Character"), GameActions.ToggleCharacter, L10n.T("Character")),
            (L10n.T("Skills"), GameActions.ToggleSkills, L10n.T("Skills")),
            (L10n.T("Bag"), GameActions.ToggleInventory, L10n.T("Inventory")),
            (L10n.T("Map"), GameActions.ToggleMap, L10n.T("Map")),
            (L10n.T("Menu"), GameActions.Cancel, L10n.T("Menu")),
        })
        {
            var button = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(76, 26),
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"{tip}  ({GameActions.DescribeBinding(action)})",
            };

            button.AddThemeFontSizeOverride("font_size", 11);
            button.AddThemeStyleboxOverride("normal", Button(new Color(0.16f, 0.12f, 0.08f)));
            button.AddThemeStyleboxOverride("hover", Button(new Color(0.26f, 0.20f, 0.11f)));
            button.AddThemeStyleboxOverride("pressed", Button(new Color(0.34f, 0.26f, 0.13f)));

            // The key's own action, so the click and the key are one and the same.
            var sent = action;
            button.Pressed += () => Press(sent);
            grid.AddChild(button);
        }

        return grid;
    }

    private static void Press(string action)
    {
        Godot.Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Godot.Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private static StyleBoxFlat Button(Color fill) => new()
    {
        BgColor = fill,
        BorderColor = new Color(0.55f, 0.45f, 0.26f),
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    // ------------------------------------------------------------------ refresh

    private void RefreshExperience()
    {
        if (_character is null) return;

        var p = _character.Progression;

        _level.Text = p.Level.ToString();
        _orbs.Progress = p.IsMaxLevel ? 1f : (float)p.LevelProgress;
        _expText.Text = p.IsMaxLevel
            ? L10n.T("max level")
            : L10n.F("{0:0.0}%  ·  {1:N0} / {2:N0}", p.LevelProgress * 100, p.Experience, p.ExperienceForNextLevel);
    }

    private void RefreshFlask()
    {
        if (_flask is null) return;

        _flaskCharges.Text = $"{_flask.Charges}/{_flask.MaxCharges}";
        _flaskCharges.AddThemeColorOverride("font_color", _flask.Charges > 0 ? Text : new Color(0.9f, 0.4f, 0.35f));
    }

    public override void _Process(double delta)
    {
        if (_player is null || !IsInstanceValid(_player)) return;

        // Both pools move every frame — regeneration, drains — so they are read, not signalled.
        _health.Value = _player.Health.Fraction;
        _mana.Value = _player.Mana.Fraction;
        _healthText.Text = _player.Health.ToString();
        _manaText.Text = _player.Mana.ToString();

        // Status effects must be visible or they read as unexplained damage and sluggishness.
        if (_player.Statuses.Count == 0)
        {
            _statuses.Text = "";
            return;
        }

        var names = new List<string>();

        foreach (var effect in _player.Statuses.Active)
        {
            names.Add(effect.Stacks > 1
                ? L10n.F("{0} x{1} ({2:F0}s)", Words.Of(effect.Kind), effect.Stacks, effect.Remaining)
                : L10n.F("{0} ({1:F0}s)", Words.Of(effect.Kind), effect.Remaining));
        }

        _statuses.Text = string.Join("  ", names);
    }
}
