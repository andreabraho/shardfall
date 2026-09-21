using Godot;
using Kiln.Core.Combat;

namespace Kiln.Game.Player;

/// <summary>
/// The shared destination-move core (D3/FR-1.7). Click-to-move and WASD both drive this one
/// motor, so speed, acceleration, facing and animation behave identically in either scheme
/// and only the source of the desired direction differs.
/// </summary>
/// <remarks>
/// <para><b>The rule that makes click-to-move feel good:</b> movement is never gated on
/// facing. The body starts moving on the same frame as the click and the visual rotates to
/// catch up. Waiting to turn before moving is the single biggest cause of "sluggish"
/// click-to-move, and it is why the original feels dated.</para>
/// </remarks>
public partial class PlayerMotor : CharacterBody3D
{
    public enum MoveMode
    {
        /// <summary>Pathing to a commanded point (click-to-move).</summary>
        Destination,

        /// <summary>Direct steering from a held direction (WASD).</summary>
        Direction,
    }

    private NavigationAgent3D _agent = null!;
    private Node3D? _visual;

    private Vector3 _directionInput = Vector3.Zero;
    private double _stuckTimer;
    private double _repathCooldown;

    /// <summary>Metres/second. Sourced from the same constant the telegraph validator uses,
    /// so movement speed and BAL-03's escape maths can never drift apart.</summary>
    [Export] public float MoveSpeed { get; set; } = (float)PlayerConstants.BaseMoveSpeed;

    /// <summary>Very high on purpose: near-instant response without a visible snap.</summary>
    [Export] public float Acceleration { get; set; } = 60f;

    [Export] public float Deceleration { get; set; } = 80f;

    /// <summary>Visual turn rate, radians/second. Does not gate movement.</summary>
    [Export] public float TurnSpeed { get; set; } = 18f;

    [Export] public float Gravity { get; set; } = 24f;

    public MoveMode Mode { get; private set; } = MoveMode.Destination;

    /// <summary>
    /// Blocks movement without clearing the order. Set by Guard Stance, which trades
    /// mobility for damage reduction (decision D4).
    /// </summary>
    public bool MovementLocked { get; set; }

    private double _heldFor;

    /// <summary>
    /// Holds the character in place for a moment: a windup, a skill (REF-01). Orders given
    /// meanwhile are kept and carried out once it ends, so a click during a swing is not lost.
    /// </summary>
    public void HoldFor(double seconds) => _heldFor = System.Math.Max(_heldFor, seconds);

    /// <summary>True while a windup or a skill holds the character still.</summary>
    public bool IsHeld => _heldFor > 0;

    public bool IsMoving => Velocity with { Y = 0 } != Vector3.Zero;

    public Vector3 Destination => _agent.TargetPosition;

    /// <summary>True while a click-to-move order is still being executed.</summary>
    public bool HasActiveOrder => Mode == MoveMode.Destination && !_agent.IsNavigationFinished();

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _visual = GetNodeOrNull<Node3D>("VisualRoot");

        // Tuned for responsiveness: stop close to the target without orbiting it, and
        // advance to the next corner early so paths round off instead of zig-zagging.
        _agent.PathDesiredDistance = 0.5f;
        _agent.TargetDesiredDistance = 0.4f;

        // No other agents yet; avoidance adds latency for nothing.
        _agent.AvoidanceEnabled = false;
    }

    /// <summary>Issues a move order to a world point (click-to-move).</summary>
    public void CommandMoveTo(Vector3 worldPoint)
    {
        Mode = MoveMode.Destination;
        _agent.TargetPosition = worldPoint;
        _stuckTimer = 0;
    }

    /// <summary>Sets a steering direction (WASD). Zero returns control to the destination core.</summary>
    public void CommandMoveDirection(Vector3 direction)
    {
        _directionInput = direction.LengthSquared() > 0.0001f
            ? direction.Normalized()
            : Vector3.Zero;

        if (_directionInput != Vector3.Zero)
        {
            Mode = MoveMode.Direction;
        }
        else if (Mode == MoveMode.Direction)
        {
            // Released the keys: stop here rather than resuming an old click order,
            // which would feel like the character disobeying.
            Mode = MoveMode.Destination;
            _agent.TargetPosition = GlobalPosition;
        }
    }

    public void Stop()
    {
        Mode = MoveMode.Destination;
        _directionInput = Vector3.Zero;
        _agent.TargetPosition = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        _heldFor = System.Math.Max(0, _heldFor - delta);

        var desired = MovementLocked || _heldFor > 0
            ? Vector3.Zero
            : Mode == MoveMode.Direction
                ? _directionInput
                : DirectionAlongPath();

        var targetVelocity = desired * MoveSpeed;
        var horizontal = Velocity with { Y = 0 };

        var rate = targetVelocity.LengthSquared() > horizontal.LengthSquared()
            ? Acceleration
            : Deceleration;

        horizontal = horizontal.MoveToward(targetVelocity, rate * (float)delta);

        // Held for a windup or a skill: planted at once, not braking into place (REF-01).
        if (_heldFor > 0) horizontal = Vector3.Zero;

        var vertical = IsOnFloor() ? 0f : Velocity.Y - (Gravity * (float)delta);
        Velocity = horizontal with { Y = vertical };

        MoveAndSlide();
        FaceMovement(delta);
        DetectStuck(delta, desired);
    }

    private Vector3 DirectionAlongPath()
    {
        if (_agent.IsNavigationFinished()) return Vector3.Zero;

        var next = _agent.GetNextPathPosition();
        var toNext = (next - GlobalPosition) with { Y = 0 };

        return toNext.LengthSquared() > 0.0001f ? toNext.Normalized() : Vector3.Zero;
    }

    /// <summary>
    /// Which way the character is actually facing, on the ground plane.
    /// </summary>
    /// <remarks>
    /// Read from the visual rather than from this body, which never turns: the body keeps
    /// its spawn rotation for the whole game and only <c>VisualRoot</c> swings. Anything that
    /// asked the body which way it was pointing got the same answer forever — which is how
    /// the attack key ended up swinging north whatever the player could see.
    /// </remarks>
    public Vector3 Facing
    {
        get
        {
            if (_visual is null) return -GlobalBasis.Z;

            var yaw = _visual.Rotation.Y;

            return new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
        }
    }

    /// <summary>Turns the character to look along a direction, immediately.</summary>
    /// <remarks>
    /// Snapped rather than eased, because its caller is an attack: a swing that lands before
    /// the character has finished turning points somewhere nobody aimed at.
    /// </remarks>
    public void FaceTowards(Vector3 direction)
    {
        var flat = direction with { Y = 0 };

        if (_visual is null || flat.LengthSquared() < 0.0001f) return;

        flat = flat.Normalized();

        _visual.Rotation = _visual.Rotation with { Y = Mathf.Atan2(-flat.X, -flat.Z) };
    }

    private void FaceMovement(double delta)
    {
        if (_visual is null) return;

        var horizontal = Velocity with { Y = 0 };
        if (horizontal.LengthSquared() < 0.04f) return;

        var targetYaw = Mathf.Atan2(-horizontal.X, -horizontal.Z);
        var current = _visual.Rotation.Y;
        var next = Mathf.LerpAngle(current, targetYaw, TurnSpeed * (float)delta);

        _visual.Rotation = _visual.Rotation with { Y = next };
    }

    /// <summary>
    /// Recovers from the classic click-to-move failure: an order that cannot complete
    /// leaves the character shuffling against geometry forever. Re-path once, then give up
    /// and stop, because a character that quietly stops is far less annoying than one that
    /// vibrates against a wall.
    /// </summary>
    private void DetectStuck(double delta, Vector3 desired)
    {
        _repathCooldown -= delta;

        if (!HasActiveOrder || desired == Vector3.Zero)
        {
            _stuckTimer = 0;
            return;
        }

        var speed = (Velocity with { Y = 0 }).Length();
        if (speed > MoveSpeed * 0.15f)
        {
            _stuckTimer = 0;
            return;
        }

        _stuckTimer += delta;

        if (_stuckTimer > 0.5 && _repathCooldown <= 0)
        {
            // Nudge the agent to recompute against current geometry.
            _agent.TargetPosition = _agent.TargetPosition;
            _repathCooldown = 0.75;
            return;
        }

        if (_stuckTimer > 1.5)
        {
            Stop();
            _stuckTimer = 0;
        }
    }
}
