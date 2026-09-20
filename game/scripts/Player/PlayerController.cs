using Godot;
using Kiln.Game.Foundation;
using Kiln.Game.Input;
using Kiln.Game.World;

namespace Kiln.Game.Player;

/// <summary>
/// Turns input into move orders. Click-to-move and WASD are both live at once, feeding the
/// same motor (MOV-09).
/// </summary>
/// <remarks>
/// There used to be a mode switch on F4. A switch asks the player to decide in advance which
/// hand they are going to use, which is not a decision anybody makes in advance — they reach
/// for whichever suits the moment, and the game should follow rather than ask.
/// </remarks>
public partial class PlayerController : Node
{
    /// <summary>
    /// How often a held mouse button re-issues the move order. 16 Hz is frequent enough to
    /// feel continuous while dragging, without re-pathing every frame.
    /// </summary>
    private const double HoldRepeatInterval = 1.0 / 16.0;

    /// <summary>Re-issue early if the cursor moved this far, so fast drags stay responsive.</summary>
    private const float HoldRepeatDistance = 0.35f;

    private PlayerMotor _motor = null!;
    private Camera3D _camera = null!;
    private ClickMarker? _marker;
    private CursorManager? _cursor;

    private double _holdTimer;
    private Vector3 _lastOrderPoint;
    private bool _wasapHeld;
    private bool _steering;
    private PlayerCombat? _combat;

    [Export] public NodePath MotorPath { get; set; } = "..";
    [Export] public NodePath MarkerPath { get; set; } = "";

    public override void _Ready()
    {
        _motor = GetNode<PlayerMotor>(MotorPath);
        _camera = GetViewport().GetCamera3D();
        _combat = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat");

        if (!MarkerPath.IsEmpty)
        {
            _marker = GetNodeOrNull<ClickMarker>(MarkerPath);
        }

        _marker ??= GetTree().Root.FindChild("ClickMarker", recursive: true, owned: false) as ClickMarker;
        _cursor = GetTree().Root.FindChild("CursorManager", recursive: true, owned: false) as CursorManager;

        Debug.DebugOverlay.Register("player", this, () =>
            $"mode={_motor.Mode} moving={_motor.IsMoving} order={_motor.HasActiveOrder} " +
            $"speed={(_motor.Velocity with { Y = 0 }).Length():F1}");
    }

    public override void _Process(double delta)
    {
        // The camera can be swapped at runtime (debug cameras, cutscenes later).
        _camera = GetViewport().GetCamera3D() ?? _camera;

        // A panel owns the mouse while it is open. Movement polls the button directly rather
        // than going through _UnhandledInput, so without this check clicking a button in the
        // inventory also walks the character across the arena.
        if (UI.UiState.ModalOpen)
        {
            // Cleared so reopening does not read the next click as the continuation of a drag.
            _wasapHeld = false;
            _holdTimer = 0;
            return;
        }

        // The keys are asked first, so a hand already on WASD overrides whatever the last
        // click ordered. Deciding in advance which of the two you are using is a decision a
        // player should never have to make; the one they are making right now is the answer.
        var steering = Steering();

        if (steering != Vector3.Zero)
        {
            // Walking away under your own power abandons the fight, exactly as clicking the
            // ground does. Otherwise combat would re-issue its approach order every physics
            // frame and the character would fight the hand holding the key.
            if (!_steering)
            {
                _combat?.ClearTarget();
                _steering = true;
            }

            _motor.CommandMoveDirection(steering);

            // So releasing the keys mid-drag is not read as the continuation of that drag.
            _wasapHeld = false;
            _holdTimer = 0;

            return;
        }

        if (_steering)
        {
            _motor.CommandMoveDirection(Vector3.Zero);
            _steering = false;
        }

        HandleClickToMove(delta);
    }

    /// <summary>
    /// The WASD direction, in world space, or zero when no key is down.
    /// </summary>
    /// <remarks>
    /// Relative to the camera rather than to the world or the character: with the camera on
    /// the right mouse button, the direction the player is looking is the direction they
    /// mean, and holding W through a turn should curve rather than carry on north.
    /// </remarks>
    private Vector3 Steering()
    {
        var input = Godot.Input.GetVector(
            GameActions.MoveLeft, GameActions.MoveRight,
            GameActions.MoveForward, GameActions.MoveBack);

        if (input == Vector2.Zero) return Vector3.Zero;

        var basis = _camera.GlobalTransform.Basis;
        var forward = -(basis.Z with { Y = 0 }).Normalized();
        var right = (basis.X with { Y = 0 }).Normalized();

        return (right * input.X) + (forward * input.Y);
    }

    private void HandleClickToMove(double delta)
    {
        var held = Godot.Input.IsActionPressed(GameActions.MoveCommand);

        if (!held)
        {
            _wasapHeld = false;
            _holdTimer = 0;
            return;
        }

        var justPressed = !_wasapHeld;
        _wasapHeld = true;
        _holdTimer -= delta;

        if (!justPressed && _holdTimer > 0) return;

        if (!TryPickGroundPoint(out var point, out var hitLayer, out var hitBody)) return;

        // Hold-to-continue: skip redundant orders unless the cursor has actually moved.
        if (!justPressed && point.DistanceTo(_lastOrderPoint) < HoldRepeatDistance)
        {
            _holdTimer = HoldRepeatInterval;
            return;
        }

        // Clicking an enemy is an attack order; clicking the ground abandons the target.
        if (justPressed && (hitLayer & Layers.Enemy) != 0 && hitBody is not null)
        {
            var enemy = hitBody.GetNodeOrNull<Combat.Combatant>("Combatant");
            if (enemy is { IsAlive: true })
            {
                _combat?.CommandAttack(enemy);
                _marker?.Flash(point);
                _lastOrderPoint = point;
                _holdTimer = HoldRepeatInterval;
                return;
            }
        }

        if (justPressed) _combat?.ClearTarget();

        _motor.CommandMoveTo(point);
        _lastOrderPoint = point;
        _holdTimer = HoldRepeatInterval;

        // Only flash the marker on a fresh click; during a drag it would strobe.
        if (justPressed)
        {
            _marker?.Flash(point);
        }
    }

    /// <summary>Raycasts from the cursor into the world. False when the cursor is over the sky.</summary>
    private bool TryPickGroundPoint(out Vector3 point, out uint hitLayer, out CollisionObject3D? hitBody)
    {
        point = Vector3.Zero;
        hitLayer = 0;
        hitBody = null;

        var mouse = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var to = from + (_camera.ProjectRayNormal(mouse) * 1000f);

        var query = PhysicsRayQueryParameters3D.Create(from, to, Layers.ClickTargets);
        query.CollideWithAreas = false;

        var hit = GetViewport().World3D.DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;

        point = hit["position"].AsVector3();

        if (hit["collider"].AsGodotObject() is CollisionObject3D body)
        {
            hitLayer = body.CollisionLayer;
            hitBody = body;
        }

        return true;
    }
}
