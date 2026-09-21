using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Game.Combat;
using Kiln.Game.Foundation;
using Kiln.Game.Input;

namespace Kiln.Game.Player;

/// <summary>
/// The Warrior's basic attack (REF-01, decided 2026-09-21 after the Metin2 model).
/// </summary>
/// <remarks>
/// A state machine on one target:
/// <code>
///   idle → target selected (click) → approach (walk into reach) → in reach
///        → windup (standing still, turning to follow) → hit check (reach, line of sight, alive)
///        → damage or miss → recovery → the next attack when the timer allows, or idle
/// </code>
/// The timer is <see cref="AttackCycle"/> and comes from attack speed alone. The animation
/// follows it; moving during the recovery cuts the animation short but never the timer.
/// <para>
/// Every blow is an area blow, as in Metin2: it lands on the target and on every other enemy in
/// an arc in front of the Warrior, within reach, at full damage. The target decides where the
/// Warrior faces and walks; the arc decides who is hit. Nothing is knocked back — a hit makes
/// each enemy flinch where it stands.
/// </para>
/// <para>
/// Space, with no click, strikes in front of the character once, without locking on. Held, it
/// keeps striking on the same timer.
/// </para>
/// </remarks>
public partial class PlayerCombat : Node
{
    /// <summary>
    /// Extra reach allowed at the moment of the hit. The approach stops at <see cref="AttackRange"/>,
    /// and a target that shuffles half a step during the windup is still in reach of a sword.
    /// </summary>
    private const float HitTolerance = 0.6f;

    /// <summary>How wide the blow sweeps in front of the Warrior. Everything inside is hit.</summary>
    private const float ArcDegrees = 120f;

    private readonly AttackCycle _cycle = new();

    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private Combatant? _target;
    private Combatant? _swingTarget;
    private TargetRing? _ring;
    private bool _committed;

    [Export] public float AttackRange { get; set; } = 2.4f;

    public Combatant? Target => _target;

    public AttackPhase Phase => _cycle.Phase;

    [Signal] public delegate void TargetChangedEventHandler();

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        Debug.DebugOverlay.Register("combat", this, () =>
            $"{_cycle.Phase.ToString().ToLowerInvariant()}  next in {_cycle.Cooldown:F2}s  "
            + $"({_self.Stats.AttacksPerSecond:F2}/s)");
    }

    // ------------------------------------------------------------------ targeting

    /// <summary>A click on an enemy: walk to it and keep attacking it.</summary>
    public void CommandAttack(Combatant enemy)
    {
        if (!enemy.IsAlive) return;

        Mark(enemy);
        _committed = true;
    }

    private void Mark(Combatant enemy)
    {
        SetTargetBarForced(false);

        _target = enemy;
        _committed = false;

        SetTargetBarForced(true);

        _ring ??= GetTree().Root.FindChild("TargetRing", recursive: true, owned: false) as TargetRing;
        _ring?.Follow(enemy.Body);

        EmitSignal(SignalName.TargetChanged);
    }

    public void ClearTarget()
    {
        if (_target is null) return;

        SetTargetBarForced(false);

        _target = null;
        _committed = false;
        _ring?.Follow(null);

        EmitSignal(SignalName.TargetChanged);
    }

    private void SetTargetBarForced(bool forced)
    {
        if (_target is null || !IsInstanceValid(_target)) return;

        var bar = _target.Body.GetNodeOrNull<HealthBar3D>("HealthBar3D");

        if (bar is null) return;

        bar.ForceVisible = forced;
        bar.Refresh();
    }

    // ------------------------------------------------------------------ the cycle

    public override void _PhysicsProcess(double delta)
    {
        if (_cycle.Tick(delta)) HitCheck();

        if (!_self.IsAlive)
        {
            _cycle.Interrupt();
            ClearTarget();
            return;
        }

        // A stun takes the blow being drawn; the timer keeps running.
        if (_self.Statuses.IsStunned)
        {
            _cycle.Interrupt();
            return;
        }

        if (_cycle.Phase == AttackPhase.Windup)
        {
            FaceSwingTarget();
            return;
        }

        // Walking off during the follow-through: the animation gives way, the timer does not.
        if (_cycle.Phase == AttackPhase.Recovery && _motor.IsMoving)
        {
            _cycle.CancelRecovery();
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.CancelSwing();
        }

        if (Godot.Input.IsActionPressed(GameActions.Attack) && !UI.UiState.ModalOpen && _cycle.CanStart)
        {
            StrikeInFront();
            return;
        }

        FollowTarget();
    }

    private void FollowTarget()
    {
        if (_target is null) return;

        if (!IsInstanceValid(_target) || !_target.IsAlive)
        {
            // Only a commitment is worth stopping for: the character was walking to this
            // enemy, so standing still on arrival is right.
            var chased = _committed;

            ClearTarget();

            if (chased) _motor.Stop();

            return;
        }

        if (!_committed) return;

        var body = _target.Body;
        var distance = _motor.GlobalPosition.DistanceTo(body.GlobalPosition);

        if (distance > AttackRange)
        {
            // Approach: re-issued every frame so a moving target is followed.
            _motor.CommandMoveTo(body.GlobalPosition);
            return;
        }

        if (_motor.HasActiveOrder) _motor.Stop();

        _motor.FaceTowards(body.GlobalPosition - _motor.GlobalPosition);

        if (_cycle.CanStart) BeginSwing(_target);
    }

    /// <summary>Starts a blow at this enemy, or at nothing — a swing into the air still takes its time.</summary>
    private void BeginSwing(Combatant? victim)
    {
        var windup = _cycle.TryStart(_self.Stats.AttacksPerSecond);

        if (windup <= 0) return;

        _swingTarget = victim;

        _motor.Stop();
        _motor.HoldFor(windup);

        var direction = victim is null
            ? _motor.Facing
            : victim.Body.GlobalPosition - _motor.GlobalPosition;

        _motor.FaceTowards(direction);
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Swing(direction);

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndSwing, _motor.GlobalPosition);
    }

    private void FaceSwingTarget()
    {
        if (_swingTarget is null || !IsInstanceValid(_swingTarget) || !_swingTarget.IsAlive) return;

        _motor.FaceTowards(_swingTarget.Body.GlobalPosition - _motor.GlobalPosition);
    }

    /// <summary>
    /// The moment the blow lands: who is there to be hit?
    /// </summary>
    /// <remarks>
    /// Reach, line of sight and life are checked now, not when the swing started. The target is
    /// hit if it is still in reach even when it has stepped a little out of the arc — it was
    /// what the player aimed at — and every other enemy in the arc is hit with it. Whoever
    /// stepped out of reach or behind a wall during the windup is simply not hit; nothing is
    /// shown, because nothing connected. "Miss" is only for a hit that connected and was evaded.
    /// </remarks>
    private void HitCheck()
    {
        var aimed = _swingTarget;

        _swingTarget = null;

        var origin = _motor.GlobalPosition;
        var reach = AttackRange + HitTolerance;
        var victims = new System.Collections.Generic.List<Combatant>();

        if (aimed is not null && IsInstanceValid(aimed) && aimed.IsAlive
            && origin.DistanceTo(aimed.Body.GlobalPosition) <= reach)
        {
            victims.Add(aimed);
        }

        // Centred on the target when there is one: the character may still be turning to it.
        var forward = victims.Count > 0
            ? (victims[0].Body.GlobalPosition - origin) with { Y = 0 }
            : _motor.Facing;

        if (forward.LengthSquared() < 0.0001f) forward = _motor.Facing;

        foreach (var other in AreaQuery.Cone(_motor, origin, forward.Normalized(), reach, ArcDegrees))
        {
            if (other.IsAlive && !victims.Contains(other)) victims.Add(other);
        }

        foreach (var victim in victims)
        {
            if (LineOfSight(victim.Body)) victim.TakeAttack(_self);
        }
    }

    private bool LineOfSight(Node3D target)
    {
        var from = _motor.GlobalPosition + (Vector3.Up * 1.2f);
        var to = target.GlobalPosition + (Vector3.Up * 1.0f);
        var query = PhysicsRayQueryParameters3D.Create(from, to, Layers.World);

        query.CollideWithAreas = false;

        return _motor.GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    /// <summary>
    /// Space: one blow in front, without locking on. It turns to the nearest enemy in the arc,
    /// if there is one, and the arc hits everything else there too.
    /// </summary>
    private void StrikeInFront()
    {
        var origin = _motor.GlobalPosition;
        var facing = _motor.Facing;

        var victim = AreaQuery.Cone(_motor, origin, facing, AttackRange + HitTolerance, ArcDegrees)
            .Where(c => c.IsAlive)
            .OrderBy(c => origin.DistanceTo(c.Body.GlobalPosition))
            .FirstOrDefault();

        BeginSwing(victim);
    }
}
