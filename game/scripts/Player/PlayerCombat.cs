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
/// <para>
/// Every fourth blow sweeps: the same damage, but everything it catches is thrown back. It is
/// read from the creatures flying, not from a shape on the ground. A boss is too heavy to move
/// and only takes the hit.
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

    /// <summary>One blow in four throws back what it hits.</summary>
    private const int SweepEvery = 4;

    [Export] public float SweepSpeed { get; set; } = 11f;

    [Export] public double SweepSeconds { get; set; } = 0.35;

    /// <summary>Blows thrown in this fight. Every fourth one sweeps.</summary>
    private int _blow;

    /// <summary>Seconds since the last blow. Long enough, and the count starts again.</summary>
    private double _idleFor;

    /// <summary>A pause this long means the next blow opens a new fight, and a new count.</summary>
    private const double ForgetAfter = 2.5;

    private readonly AttackCycle _cycle = new();

    private PlayerMotor _motor = null!;
    private SkillCaster? _skills;
    private Combatant _self = null!;
    private Combatant? _target;
    private Combatant? _swingTarget;
    private TargetRing? _ring;
    private bool _committed;

    /// <summary>How far a blade reaches.</summary>
    [Export] public float MeleeRange { get; set; } = 2.4f;

    /// <summary>How far the Archer's arrow goes (CLS-04).</summary>
    public const float BowRange = 13f;

    /// <summary>
    /// True for a class whose ordinary blow is a shot at one target, not an arc of a blade: the
    /// Archer. The Dragonseer strikes with its fan, hand to hand, as the original's does
    /// (2026-10-02, at your call): its magic is in its skills.
    /// </summary>
    public static bool Ranged => PlayerProfile.Class is Kiln.Core.Foundation.CharacterClass.Archer;

    /// <summary>How far the ordinary blow of the class played reaches.</summary>
    public float AttackRange => PlayerProfile.Class switch
    {
        Kiln.Core.Foundation.CharacterClass.Archer => BowRange,
        _ => MeleeRange,
    };

    public Combatant? Target => _target;

    public AttackPhase Phase => _cycle.Phase;

    [Signal] public delegate void TargetChangedEventHandler();

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _skills = GetParent().GetNodeOrNull<SkillCaster>("SkillCaster");
        _self = GetParent().GetNode<Combatant>("Combatant");

        Debug.DebugOverlay.Register("combat", this, () =>
            $"{_cycle.Phase.ToString().ToLowerInvariant()}  next in {_cycle.Cooldown:F2}s  "
            + $"({AttacksPerSecond:F2}/s)");
    }

    // ------------------------------------------------------------------ targeting

    /// <summary>A click on an enemy: walk to it and keep attacking it.</summary>
    public void CommandAttack(Combatant enemy)
    {
        if (!enemy.IsAlive) return;

        _skills?.CancelApproach();

        Mark(enemy);
        _committed = true;
    }

    /// <summary>
    /// A right click on an enemy (2026-10-01, at your call): chosen, not attacked. Skills go at
    /// it; the character stays where it is. Choosing the one already being fought changes nothing.
    /// </summary>
    public void Select(Combatant enemy)
    {
        if (!enemy.IsAlive) return;

        if (ReferenceEquals(enemy, _target)) return;

        Mark(enemy);
    }

    /// <summary>
    /// Stops walking to the target and striking it, and keeps it chosen: walking off, picking
    /// something up or going unseen ends the fight, not the choice.
    /// </summary>
    public void Disengage()
    {
        _committed = false;
        _skills?.CancelApproach();
    }

    /// <summary>
    /// How far the Archer looks for something to shoot when nothing is chosen (2026-10-07, at your
    /// call): further than the bow reaches, so it walks up to what is near rather than doing nothing.
    /// </summary>
    public const float SeekRadius = 25f;

    /// <summary>
    /// The target chosen, if it still lives; otherwise the nearest enemy within
    /// <paramref name="within"/>, chosen now. Null when there is nothing.
    /// </summary>
    public Combatant? TargetOrNearest(float within)
    {
        if (_target is not null && IsInstanceValid(_target) && _target.IsAlive) return _target;

        var origin = _motor.GlobalPosition;
        var nearest = AreaQuery.Sphere(_motor, origin, within)
            .Where(c => c.IsAlive)
            .OrderBy(c => LineOfSight(c.Body) ? 0 : 1)
            .ThenBy(c => origin.DistanceTo(c.Body.GlobalPosition))
            .FirstOrDefault();

        if (nearest is not null) Mark(nearest);

        return nearest;
    }

    /// <summary>True when nothing of the world stands between the character and this one.</summary>
    public bool InSight(Combatant other) => LineOfSight(other.Body);

    /// <summary>How far a chosen target can be left behind before the choice is dropped.</summary>
    private const float ForgetBeyond = 45f;

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

        if (Godot.Input.IsActionPressed(GameActions.Attack) && !UI.UiState.ModalOpen && _cycle.CanStart && StrikeInFront())
        {
            return;
        }

        _idleFor += delta;

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

        var body = _target.Body;
        var distance = _motor.GlobalPosition.DistanceTo(body.GlobalPosition);

        // A target only chosen is kept until it is left far behind.
        if (!_committed)
        {
            if (distance > ForgetBeyond) ClearTarget();

            return;
        }

        // A shot also wants the target in sight: round the corner first.
        if (distance > AttackRange || (Ranged && !LineOfSight(body)))
        {
            // Approach: re-issued every frame so a moving target is followed.
            _motor.CommandMoveTo(body.GlobalPosition);
            return;
        }

        if (_motor.HasActiveOrder) _motor.Stop();

        _motor.FaceTowards(body.GlobalPosition - _motor.GlobalPosition);

        if (_cycle.CanStart) BeginSwing(_target);
    }

    /// <summary>Blows a second as the stats give them, quickened by a frenzy (REF-21).</summary>
    private double AttacksPerSecond => _self.Stats.AttacksPerSecond * _self.Statuses.AttackSpeedMultiplier;

    /// <summary>Starts a blow at this enemy, or at nothing — a swing into the air still takes its time.</summary>
    private void BeginSwing(Combatant? victim)
    {
        // A skill being cast owns the body; the next blow waits for it to finish (REF-22).
        if (_skills?.IsCasting == true) return;

        var windup = _cycle.TryStart(AttacksPerSecond);

        if (windup <= 0) return;

        // A fresh fight starts its count at one, so the sweep never lands on the first blow
        // just because of how the last fight happened to end.
        if (_idleFor > ForgetAfter) _blow = 0;

        _idleFor = 0;
        _swingTarget = victim;

        _motor.Stop();
        _motor.HoldFor(windup);

        var direction = victim is null
            ? _motor.Facing
            : victim.Body.GlobalPosition - _motor.GlobalPosition;

        _motor.FaceTowards(direction);
        // The blow this swing will be, so the fourth — the sweep — looks like one (REF-22).
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Blow(direction, _blow + 1, windup, AttackCycle.IntervalFor(AttacksPerSecond));

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

        if (Ranged)
        {
            Loose(aimed);
            return;
        }

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

        _blow++;

        var sweeps = _blow % SweepEvery == 0;

        var landed = 0;
        var thrown = 0;

        foreach (var victim in victims)
        {
            if (!LineOfSight(victim.Body)) continue;

            landed++;

            // Out of the Shadow Veil (CLS-03): the blow that ends it is worth that much more.
            var veil = _self.Statuses.MagnitudeOf(StatusKind.Stealth);

            _self.Statuses.Remove(StatusKind.Stealth);
            victim.TakeAttack(_self, weaponCoef: 1 + veil);

            // Thrown back, unless it is a boss: the sweep is crowd control, not a way to
            // shove a boss out of its own arena.
            if (sweeps && victim.Body is EnemyBrain brain && brain.StunResist < 1)
            {
                brain.Shove(origin, SweepSpeed, SweepSeconds);
                thrown++;
            }
        }

        // Debug builds only: enough to see the rhythm and the sweep in the console while playing.
        if (OS.IsDebugBuild())
        {
            GD.Print($"[combat] blow {_blow}{(sweeps ? " — sweep" : "")}: {landed} hit"
                + (sweeps ? $", {thrown} thrown back" : ""));
        }
    }

    /// <summary>
    /// The shot leaves (CLS-04): at the one it was aimed at, if it is still in range and in
    /// sight, and at nothing else. Aimed at nothing,
    /// it flies its length and is lost.
    /// </summary>
    private void Loose(Combatant? aimed)
    {
        var origin = _motor.GlobalPosition;
        var colour = new Color("fff3d6");
        var hit = aimed is not null && IsInstanceValid(aimed) && aimed.IsAlive
            && origin.DistanceTo(aimed.Body.GlobalPosition) <= AttackRange + HitTolerance + 1.0f
            && LineOfSight(aimed.Body);

        _blow++;

        var to = hit ? aimed!.Body.GlobalPosition : origin + (_motor.Facing * AttackRange);

        SkillFx.Arrow(this, origin, to, colour);

        if (!hit) return;

        var veil = _self.Statuses.MagnitudeOf(StatusKind.Stealth);

        _self.Statuses.Remove(StatusKind.Stealth);
        aimed!.TakeAttack(_self, weaponCoef: 1 + veil);
    }

    private bool LineOfSight(Node3D target)

    {
        var from = _motor.GlobalPosition + (Vector3.Up * 1.2f);
        var to = target.GlobalPosition + (Vector3.Up * 1.0f);
        using var query = PhysicsRayQueryParameters3D.Create(from, to, Layers.World);

        query.CollideWithAreas = false;

        using var hit = _motor.GetWorld3D().DirectSpaceState.IntersectRay(query);

        return hit.Count == 0;
    }

    /// <summary>
    /// Space: one blow in front, without locking on. It turns to the nearest enemy in the arc,
    /// if there is one, and the arc hits everything else there too. False when the Archer walks
    /// to its target instead, and the approach is left to <see cref="FollowTarget"/>.
    /// </summary>
    private bool StrikeInFront()
    {
        var origin = _motor.GlobalPosition;
        var facing = _motor.Facing;

        // The Archer (2026-10-07, at your call): a shot is not an arc, so Space looses at the target
        // chosen, or — with none — chooses the nearest enemy round it. Out of reach or out of sight,
        // it walks up to it and shoots from there, as a click on it would.
        if (Ranged)
        {
            if (TargetOrNearest(SeekRadius) is not { } chosen)
            {
                BeginSwing(null);
                return true;
            }

            if (origin.DistanceTo(chosen.Body.GlobalPosition) <= AttackRange + HitTolerance && LineOfSight(chosen.Body))
            {
                BeginSwing(chosen);
                return true;
            }

            if (!_committed) CommandAttack(chosen);

            return false;
        }

        var victim = AreaQuery.Cone(_motor, origin, facing, AttackRange + HitTolerance, Ranged ? 50f : ArcDegrees)
            .Where(c => c.IsAlive)
            .OrderBy(c => origin.DistanceTo(c.Body.GlobalPosition))
            .FirstOrDefault();

        BeginSwing(victim);
        return true;
    }
}
