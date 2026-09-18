using Godot;
using Kiln.Game.Combat;

namespace Kiln.Game.Player;

/// <summary>
/// Click-to-attack (CBT-03, FR-1.2): click an enemy to close distance and auto-attack until
/// it dies, leaves range, or you give another order.
/// <para>
/// Faithful to the original's scheme (D3) — the player commits to a target rather than
/// aiming each swing.
/// </para>
/// </summary>
public partial class PlayerCombat : Node
{
    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private Combatant? _target;
    private double _swingCooldown;

    /// <summary>Melee reach. Slightly generous so chasing a moving target is not fiddly.</summary>
    [Export] public float AttackRange { get; set; } = 2.4f;

    public Combatant? Target => _target;

    [Signal] public delegate void TargetChangedEventHandler();

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        Debug.DebugOverlay.Register("combat", () =>
            _target is { IsAlive: true }
                ? $"target={_target.DisplayName} hp={_target.Health}"
                : "no target");
    }

    /// <summary>Called by PlayerController when the click landed on an enemy.</summary>
    public void CommandAttack(Combatant enemy)
    {
        if (!enemy.IsAlive) return;

        _target = enemy;
        EmitSignal(SignalName.TargetChanged);
    }

    /// <summary>Clears the target — any plain move order abandons the attack.</summary>
    public void ClearTarget()
    {
        if (_target is null) return;

        _target = null;
        EmitSignal(SignalName.TargetChanged);
    }

    public override void _PhysicsProcess(double delta)
    {
        _swingCooldown -= delta;

        if (!_self.IsAlive)
        {
            ClearTarget();
            return;
        }

        if (_target is null) return;

        if (!_target.IsAlive || !GodotObject.IsInstanceValid(_target))
        {
            ClearTarget();
            _motor.Stop();
            return;
        }

        var targetBody = _target.Body;
        var distance = _motor.GlobalPosition.DistanceTo(targetBody.GlobalPosition);

        if (distance > AttackRange)
        {
            // Walk into range. Re-issued each frame so a moving target is followed.
            _motor.CommandMoveTo(targetBody.GlobalPosition);
            return;
        }

        // In range: stop and swing on the attack-speed cadence.
        if (_motor.HasActiveOrder) _motor.Stop();

        if (_swingCooldown > 0) return;

        _swingCooldown = 1.0 / Math.Max(0.1, _self.Stats.AttacksPerSecond);

        var result = _target.TakeAttack(_self);

        if (!result.Evaded && _target.IsAlive)
        {
            // Nudge the target's bar visible even on a graze.
            _target.EmitSignal(Combatant.SignalName.HealthChanged, (float)_target.Health.Fraction);
        }
    }
}
