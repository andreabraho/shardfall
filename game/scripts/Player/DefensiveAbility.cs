using Godot;
using Kiln.Game.Combat;
using Kiln.Game.Input;

namespace Kiln.Game.Player;

/// <summary>
/// Guard Stance — the Warrior's defensive cooldown (CBT-10, decision D4).
/// <para>
/// This is the replacement for the dodge roll that click-to-move rules out. It restores the
/// "did you react in time" moment: a short, high-value window that has to be spent on the
/// right telegraph, with a cost — you cannot move while holding it, so guarding instead of
/// walking out is a real trade rather than a free button.
/// </para>
/// </summary>
public partial class DefensiveAbility : Node
{
    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private double _active;
    private double _cooldown;

    /// <summary>
    /// The skill this ability is. All of its numbers come from that definition, so Guard
    /// Stance tunes and ranks up exactly like every other skill instead of having a second,
    /// silently diverging copy of its values in code.
    /// </summary>
    [Export] public string SkillId { get; set; } = "skl_guard_stance";

    // Fallbacks, used only if the definition is missing.
    [Export] public double DamageReduction { get; set; } = 0.70;
    [Export] public double Duration { get; set; } = 2.0;
    [Export] public double CooldownSeconds { get; set; } = 10.0;
    [Export] public double ManaCost { get; set; } = 10;

    public bool IsGuarding => _active > 0;

    public double CooldownRemaining => System.Math.Max(0, _cooldown);

    [Signal] public delegate void GuardChangedEventHandler(bool guarding);

    private GuardVisual _visual = null!;

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        // Created in code rather than placed in the scene: it belongs to this ability and
        // has no meaning without it.
        _visual = new GuardVisual { Name = "GuardVisual" };

        // Deferred: the parent is still building its own children during _Ready, and a
        // direct AddChild there fails outright.
        _motor.CallDeferred(Node.MethodName.AddChild, _visual);

        // Flash on every absorbed hit — otherwise a successful guard looks like nothing
        // happened, which is the worst possible feedback for a defensive cooldown.
        _self.Damaged += (_, _, evaded) =>
        {
            if (IsGuarding && !evaded) _visual.Flash();
        };

        Debug.DebugOverlay.Register("guard", () =>
            IsGuarding ? $"GUARDING {_active:F1}s" : _cooldown > 0 ? $"cd {_cooldown:F1}s" : "ready");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(GameActions.DefensiveAbility)) return;

        TryGuard();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Current values, including whatever mastery has changed.</summary>
    private (double Reduction, double Duration, double Cooldown, double Mana) Values()
    {
        var book = GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter")?.Skills;

        if (book is null
            || !book.IsUnlocked(SkillId)
            || !GameContent.IsLoaded
            || !GameContent.Database.Skills.TryGetValue(SkillId, out var def))
        {
            return (DamageReduction, Duration, CooldownSeconds, ManaCost);
        }

        var skill = Kiln.Data.Definitions.ResolvedSkill.For(def, book.RankOf(SkillId));
        return (skill.Magnitude, skill.Duration, skill.Cooldown, skill.ManaCost);
    }

    private void TryGuard()
    {
        if (IsGuarding || _cooldown > 0 || !_self.IsAlive || _self.Statuses.IsStunned) return;

        var values = Values();

        if (!_self.Mana.TrySpend(values.Mana))
        {
            GD.Print($"[guard] not enough mana ({_self.Mana})");
            return;
        }

        _active = values.Duration;
        _cooldown = values.Cooldown;

        GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter")?.Skills.RecordUse(SkillId);

        _self.IncomingDamageMultiplier = 1.0 - values.Reduction;
        _motor.MovementLocked = true;
        _motor.Stop();
        _visual.SetGuarding(true);

        EmitSignal(SignalName.GuardChanged, true);
    }

    public override void _Process(double delta)
    {
        if (_cooldown > 0) _cooldown -= delta;

        if (_active <= 0) return;

        _active -= delta;

        // Dying mid-guard must not leave the player permanently rooted.
        if (_active > 0 && _self.IsAlive) return;

        EndGuard();
    }

    private void EndGuard()
    {
        _active = 0;
        _self.IncomingDamageMultiplier = 1.0;
        _motor.MovementLocked = false;
        _visual.SetGuarding(false);

        EmitSignal(SignalName.GuardChanged, false);
    }
}
