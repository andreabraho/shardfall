using System;
using System.Collections.Generic;
using Godot;
using Kiln.Game.Combat;
using Kiln.Game.Player;

namespace Kiln.Game.UI;

/// <summary>
/// Minimal combat HUD for the Phase 2 slice: player health and a target frame.
/// <para>
/// Built in code rather than as a scene because it is scaffolding — the real HUD (FR-12.1,
/// orbs, hotbar, buff strip) lands in Phase 8. This exists so combat can be judged at all:
/// without a health bar there is no way to tell whether a fight was close.
/// </para>
/// </summary>
public partial class CombatHud : CanvasLayer
{
    private ProgressBar _playerBar = null!;
    private ProgressBar _manaBar = null!;
    private Label _playerLabel = null!;
    private PanelContainer _targetPanel = null!;
    private ProgressBar _targetBar = null!;
    private Label _targetLabel = null!;

    private Combatant? _player;
    private PlayerCombat? _combat;
    private HealthFlask? _flask;
    private DefensiveAbility? _guard;
    private Label _flaskLabel = null!;
    private Label _manaLabel = null!;
    private Label _statusLabel = null!;
    private Label _levelLabel = null!;
    private ProgressBar _xpBar = null!;
    private PlayerCharacter? _character;

    public override void _Ready()
    {
        Layer = 10;
        BuildPlayerFrame();
        BuildTargetFrame();
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        var playerBody = GetTree().GetFirstNodeInGroup("player");
        if (playerBody is null) return;

        _player = playerBody.GetNodeOrNull<Combatant>("Combatant");
        _combat = playerBody.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        _flask = playerBody.GetNodeOrNull<HealthFlask>("HealthFlask");
        _guard = playerBody.GetNodeOrNull<DefensiveAbility>("DefensiveAbility");
        _character = playerBody.GetNodeOrNull<PlayerCharacter>("PlayerCharacter");

        if (_character is not null)
        {
            _character.ExperienceChanged += RefreshExperience;
            RefreshExperience();
        }

        if (_player is not null)
        {
            _player.HealthChanged += _ => RefreshPlayer();
            RefreshPlayer();
        }

        if (_flask is not null)
        {
            _flask.ChargesChanged += (_, _) => RefreshFlask();
            RefreshFlask();
        }
    }

    private void RefreshExperience()
    {
        if (_character is null) return;

        var p = _character.Progression;

        _levelLabel.Text = p.IsMaxLevel
            ? $"Level {p.Level} (max)"
            : $"Level {p.Level}   {p.Experience:N0} / {p.ExperienceForNextLevel:N0} xp";

        _xpBar.Value = p.LevelProgress;
    }

    private void RefreshFlask()
    {
        if (_flask is null) return;

        // Pips rather than a number: charge count has to be readable at a glance mid-fight.
        _flaskLabel.Text = $"Flask  {new string('◆', _flask.Charges)}{new string('◇', Math.Max(0, _flask.MaxCharges - _flask.Charges))}";
    }

    private void BuildPlayerFrame()
    {
        // Tall enough for everything it holds. At 96 the box was shorter than its contents,
        // so the last rows — mana among them — were squeezed to nothing and never appeared.
        var root = new MarginContainer { AnchorTop = 1, AnchorBottom = 1, OffsetTop = -210, OffsetLeft = 24, OffsetRight = 344 };
        root.AddThemeConstantOverride("margin_bottom", 24);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        _playerLabel = new Label { Text = "Health" };
        _playerBar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(320, 22) };
        _manaBar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(320, 14) };

        // Each bar is coloured for what it is. Three identically styled bars stacked on top
        // of one another are three bars the player has to decode mid-fight.
        Tint(_playerBar, new Color("c0392b"));
        Tint(_manaBar, new Color("3f7fd0"));

        _manaLabel = new Label { Text = "Mana" };
        _manaLabel.AddThemeFontSizeOverride("font_size", 12);
        _manaLabel.AddThemeColorOverride("font_color", new Color("8fb6e8"));

        _flaskLabel = new Label { Text = "Flask" };
        _statusLabel = new Label { Text = "" };
        _levelLabel = new Label { Text = "Level 1" };
        _xpBar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(320, 8) };

        box.AddChild(_statusLabel);
        box.AddChild(_levelLabel);
        box.AddChild(_xpBar);
        box.AddChild(_playerLabel);
        box.AddChild(_playerBar);
        box.AddChild(_manaLabel);
        box.AddChild(_manaBar);
        box.AddChild(_flaskLabel);
        root.AddChild(box);
        AddChild(root);
    }

    private static void Tint(ProgressBar bar, Color fill)
    {
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = fill,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        });

        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.11f, 0.13f, 0.85f),
            BorderColor = new Color(0.28f, 0.31f, 0.36f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });
    }

    private void BuildTargetFrame()
    {
        _targetPanel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -160,
            OffsetRight = 160,
            OffsetTop = 24,
            Visible = false,
        };

        var box = new VBoxContainer();
        _targetLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _targetBar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(300, 18) };

        box.AddChild(_targetLabel);
        box.AddChild(_targetBar);
        _targetPanel.AddChild(box);
        AddChild(_targetPanel);
    }

    private void RefreshPlayer()
    {
        if (_player is null) return;

        _playerBar.Value = _player.Health.Fraction;
        _manaBar.Value = _player.Mana.Fraction;
        _playerLabel.Text = _player.IsAlive
            ? $"Health  {_player.Health}"
            : "Dead — respawning";
    }

    public override void _Process(double _)
    {
        // Mana drains and regenerates continuously, so it cannot be signal-driven.
        if (_player is not null)
        {
            _manaBar.Value = _player.Mana.Fraction;
            _manaLabel.Text = $"Mana  {_player.Mana}";
        }

        _statusLabel.Text = _guard switch
        {
            { IsGuarding: true } => "GUARDING",
            { CooldownRemaining: > 0 } g => $"Guard ready in {g.CooldownRemaining:F1}s",
            _ => "Guard ready  [Space]",
        };

        // Status effects must be visible or they read as unexplained damage and sluggishness.
        if (_player is not null && _player.Statuses.Count > 0)
        {
            var names = new List<string>();

            foreach (var effect in _player.Statuses.Active)
            {
                names.Add(effect.Stacks > 1
                    ? $"{effect.Kind} x{effect.Stacks} ({effect.Remaining:F0}s)"
                    : $"{effect.Kind} ({effect.Remaining:F0}s)");
            }

            _statusLabel.Text += "     " + string.Join("  ", names);
        }

        if (_combat is null) return;

        var target = _combat.Target;

        if (target is null || !target.IsAlive || !IsInstanceValid(target))
        {
            _targetPanel.Visible = false;
            return;
        }

        _targetPanel.Visible = true;
        _targetBar.Value = target.Health.Fraction;

        // Definition names are localisation keys; strip the prefix until the string table
        // lands with UIX-06 rather than showing "$mob.mob_corrupted_wolf.name" on screen.
        _targetLabel.Text = $"{Pretty(target.DisplayName)}   {target.Health}";
    }

    private static string Pretty(string key)
    {
        if (string.IsNullOrEmpty(key)) return "Target";

        var parts = key.TrimStart('$').Split('.');
        var name = parts.Length >= 2 ? parts[^2] : parts[^1];

        return name.Replace('_', ' ').Trim();
    }
}
