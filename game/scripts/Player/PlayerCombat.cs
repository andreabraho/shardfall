using Godot;
using Kiln.Game.Combat;
using Kiln.Game.Input;

namespace Kiln.Game.Player;

/// <summary>
/// Click-to-attack (CBT-03, FR-1.2): click an enemy to close distance and auto-attack until
/// it dies, leaves range, or you give another order — and Space to swing where you face,
/// with nothing selected at all (MOV-11).
/// <para>
/// The click scheme is faithful to the original (D3): the player commits to a target rather
/// than aiming each swing. The key is the escape hatch from that commitment, for the moments
/// when the other hand is steering and there is something in the way.
/// </para>
/// </summary>
public partial class PlayerCombat : Node
{
    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private Combatant? _target;
    private TargetRing? _ring;
    private double _swingCooldown;
    private bool _committed;

    /// <summary>Melee reach. Slightly generous so chasing a moving target is not fiddly.</summary>
    [Export] public float AttackRange { get; set; } = 2.4f;

    /// <summary>
    /// Basic attacks cleave into every enemy in an arc, not just the target.
    /// <para>
    /// This is why two-handed weapons are worth using against a pack, and it is what makes
    /// pulling groups — and therefore shard encounters — work at all. Without it the whole
    /// game becomes single-target whack-a-mole.
    /// </para>
    /// </summary>
    [Export] public bool CleaveEnabled { get; set; } = true;

    /// <summary>Total arc of the cleave, centred on the target direction.</summary>
    [Export] public float CleaveAngle { get; set; } = 120f;

    /// <summary>Reach of the cleave, slightly beyond single-target range.</summary>
    [Export] public float CleaveRange { get; set; } = 3.0f;

    /// <summary>
    /// Damage secondary targets take, relative to the primary.
    /// <para>
    /// 1.0 — full damage to everything in the arc, as in the original. This is a deliberate
    /// call: falloff made two-handed crowd clearing feel weak, and full cleave is a large
    /// part of why the source game's pulls are satisfying. It does mean area damage scales
    /// hard with pack size, which is a balance lever to watch in the shard encounters rather
    /// than a reason to tax the basic attack.
    /// </para>
    /// </summary>
    [Export] public float CleaveFalloff { get; set; } = 1.0f;

    public Combatant? Target => _target;

    [Signal] public delegate void TargetChangedEventHandler();

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        Debug.DebugOverlay.Register("combat", this, () =>
            _target is { IsAlive: true }
                ? $"target={_target.DisplayName} hp={_target.Health}"
                : "no target");
    }

    /// <summary>Called by PlayerController when the click landed on an enemy.</summary>
    /// <remarks>
    /// A click is a commitment: the character walks to this enemy and keeps swinging at it
    /// without being asked again. That is what separates it from <see cref="Mark"/>.
    /// </remarks>
    public void CommandAttack(Combatant enemy)
    {
        if (!enemy.IsAlive) return;

        Mark(enemy);
        _committed = true;
    }

    /// <summary>
    /// Points the ring at an enemy without committing to chasing it.
    /// </summary>
    /// <remarks>
    /// The ring means "this is who I am fighting", and a camera-aimed swing has to be able to
    /// say that too — otherwise the ring sits on whatever was last clicked while the player
    /// turns and hits something else entirely, which is a marker that lies. Marking without
    /// committing is the distinction that lets it be honest: the swing moves the ring, but
    /// only a click makes the character walk anywhere.
    /// </remarks>
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

    /// <summary>Clears the target — any plain move order abandons the attack.</summary>
    public void ClearTarget()
    {
        if (_target is null) return;

        SetTargetBarForced(false);
        _target = null;
        _committed = false;
        _ring?.Follow(null);

        EmitSignal(SignalName.TargetChanged);
    }

    /// <summary>
    /// Keeps the target's bar on screen even at full health, so selecting an untouched
    /// enemy still shows which one is selected.
    /// </summary>
    private void SetTargetBarForced(bool forced)
    {
        if (_target is null || !IsInstanceValid(_target)) return;

        var bar = _target.Body.GetNodeOrNull<HealthBar3D>("HealthBar3D");
        if (bar is null) return;

        bar.ForceVisible = forced;
        bar.Refresh();
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
            // Only a commitment is worth stopping for. The character was walking to this
            // enemy, so standing still on arrival is right — but a marked one was never
            // being walked to, and halting there would cancel a move order the player gave
            // for their own reasons.
            var chased = _committed;

            ClearTarget();

            if (chased) _motor.Stop();

            return;
        }

        // A marked enemy is shown, not chased. Only a click asked the character to go there,
        // and a ring that started walking the player across the field would turn the attack
        // key into a move order nobody gave.
        if (!_committed) return;

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
        Swing(targetBody.GlobalPosition);
    }

    private void Swing(Vector3 targetPosition)
    {
        if (_target is null) return;

        Strike(targetPosition - _motor.GlobalPosition, _target);
    }

    /// <summary>
    /// A swing at whatever happens to be in front, with nothing selected (MOV-11).
    /// </summary>
    /// <remarks>
    /// The manual counterpart to the automatic chain above: it costs the same cooldown and
    /// does the same damage, but it goes where the character is facing rather than where a
    /// selection is, and it never walks anywhere. That makes it the attack that works while
    /// the other hand is steering — and it is the shape the four-hit chain (CBT-17) will be
    /// built on, so it is worth having the swing be a thing you press before it is a thing
    /// that counts.
    /// </remarks>
    public bool SwingForward()
    {
        if (!_self.IsAlive || _swingCooldown > 0) return false;

        _swingCooldown = 1.0 / Math.Max(0.1, _self.Stats.AttacksPerSecond);

        var aim = CameraForward();

        // The character turns to the swing rather than the swing bending to the character.
        // With the camera on the right mouse button, where the player is looking is where
        // they are aiming, and a swing that ignored that would make the camera a spectator.
        _motor.FaceTowards(aim);

        // Whatever the swing connected with becomes what the ring points at, so turning and
        // hitting something else moves the marker with you. Nothing hit clears it: a ring
        // left over from a click, while the player swings somewhere else, is the marker
        // pointing at one enemy and the sword at another.
        var hit = StrikeForward(aim);

        if (hit is { IsAlive: true }) Mark(hit);
        else ClearTarget();

        return true;
    }

    /// <summary>
    /// The camera-aimed swing, returning the nearest enemy it caught.
    /// </summary>
    /// <remarks>
    /// No primary target: whatever is in the arc is what gets hit. A selected enemy standing
    /// behind the player is not in front of them, and a key aimed by the camera that still
    /// reached it would be lying about what aiming means.
    /// </remarks>
    private Combatant? StrikeForward(Vector3 aim) => Strike(aim, null);

    /// <summary>Where the camera is looking, flattened to the ground.</summary>
    private Vector3 CameraForward()
    {
        var camera = GetViewport().GetCamera3D();

        if (camera is null) return _motor.Facing;

        var forward = -camera.GlobalTransform.Basis.Z with { Y = 0 };

        // Looking straight down leaves nothing to flatten; keep the character's own facing.
        return forward.LengthSquared() < 0.0001f ? _motor.Facing : forward.Normalized();
    }

    /// <summary>
    /// One swing: the selected enemy if there is one, and the arc either way. Returns the
    /// nearest enemy it hit, which is the one the ring should point at.
    /// </summary>
    private Combatant? Strike(Vector3 direction, Combatant? primary)
    {
        // The primary target always takes a full hit, even if the arc maths would miss it —
        // the player explicitly selected it and a whiff would read as a bug.
        primary?.TakeAttack(_self);

        var origin = _motor.GlobalPosition;
        var forward = direction with { Y = 0 };
        if (forward.LengthSquared() < 0.0001f) return primary;

        forward = forward.Normalized();

        // The swing itself, shown on every attack rather than only on a multi-hit. Against a
        // single enemy the attack was otherwise invisible — the only feedback was the
        // victim's flash — so a fight read as two figures standing still trading numbers.
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Lunge(forward);
        AoeVisual.Cone(origin, forward, CleaveRange, CleaveAngle);

        // With nothing selected the arc is the whole attack, so it swings even when cleave is
        // off: switching cleave off means an auto-attack should not splash, not that pressing
        // the attack key should do nothing.
        if (!CleaveEnabled && primary is not null) return primary;

        var nearest = primary;
        var nearestDistance = primary is null
            ? float.MaxValue
            : origin.DistanceTo(primary.Body.GlobalPosition);

        foreach (var other in AreaQuery.Cone(_motor, origin, forward, CleaveRange, CleaveAngle))
        {
            if (other == primary || !other.IsAlive) continue;

            other.TakeAttack(_self, weaponCoef: primary is null ? 1.0 : CleaveFalloff);

            // Nearest rather than first, so the ring lands on the one the player is most
            // obviously fighting rather than on whichever the physics query happened to
            // return first.
            var distance = origin.DistanceTo(other.Body.GlobalPosition);

            if (distance >= nearestDistance) continue;

            nearest = other;
            nearestDistance = distance;
        }

        return nearest;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // A panel has the player's attention; swinging behind it is never intended.
        if (UI.UiState.ModalOpen || !@event.IsActionPressed(GameActions.Attack)) return;

        SwingForward();
        GetViewport().SetInputAsHandled();
    }
}
