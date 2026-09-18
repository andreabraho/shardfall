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

    /// <summary>Fraction of damage blocked while guarding.</summary>
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

    private void TryGuard()
    {
        if (IsGuarding || _cooldown > 0 || !_self.IsAlive || _self.Statuses.IsStunned) return;

        if (!_self.Mana.TrySpend(ManaCost))
        {
            GD.Print($"[guard] not enough mana ({_self.Mana})");
            return;
        }

        _active = Duration;
        _cooldown = CooldownSeconds;

        _self.IncomingDamageMultiplier = 1.0 - DamageReduction;
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
