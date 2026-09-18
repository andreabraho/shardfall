using Godot;
using Kiln.Game.Foundation;
using Kiln.Game.Input;

namespace Kiln.Game.Camera;

/// <summary>
/// Fixed-pitch semi-isometric follow camera (D2, FR-1.6).
/// <para>
/// Node layout: CameraRig → Yaw → SpringArm3D → Camera3D. Keeping yaw on its own node
/// means camera-relative WASD input is just the yaw basis. The SpringArm's own collision
/// pull-in is disabled by default — see <see cref="CollisionPullIn"/>.
/// </para>
/// </summary>
public partial class CameraRig : Node3D
{
    private Node3D _yaw = null!;
    private SpringArm3D _arm = null!;
    private Node3D? _target;

    private float _targetZoom;
    private bool _dragging;
    private double _shake;
    private double _shakeDuration;
    private static CameraRig? _instance;

    [Export] public NodePath TargetPath { get; set; } = "";

    /// <summary>Fixed camera pitch. Not player-adjustable: it defines the game's read.</summary>
    [Export] public float PitchDegrees { get; set; } = 50f;

    [Export] public float MinZoom { get; set; } = 6f;
    [Export] public float MaxZoom { get; set; } = 18f;
    [Export] public float ZoomStep { get; set; } = 1.5f;

    /// <summary>Higher follows tighter. Exponential smoothing, so it is frame-rate independent.</summary>
    [Export] public float FollowSharpness { get; set; } = 12f;

    [Export] public float ZoomSharpness { get; set; } = 10f;

    /// <summary>Degrees per second for keyboard rotation.</summary>
    [Export] public float RotateSpeed { get; set; } = 120f;

    /// <summary>Degrees per pixel of middle-drag.</summary>
    [Export] public float DragSensitivity { get; set; } = 0.35f;

    /// <summary>Peak camera displacement from a shake, in metres.</summary>
    [Export] public float ShakeStrength { get; set; } = 0.22f;

    /// <summary>
    /// Let the SpringArm pull the camera in when geometry is behind the player.
    /// <para>
    /// Off by default, and that is a deliberate design call rather than an oversight.
    /// Pull-in makes the camera lurch closer whenever the player stands near a wall or
    /// pillar, which in a click-to-move game keeps changing the screen-to-world mapping
    /// the player aims with — clicks then land somewhere other than intended. Occlusion is
    /// handled by <see cref="CameraOccluderFade"/> instead, which keeps the distance fixed.
    /// </para>
    /// </summary>
    [Export] public bool CollisionPullIn { get; set; }

    public override void _Ready()
    {
        _yaw = GetNode<Node3D>("Yaw");
        _arm = GetNode<SpringArm3D>("Yaw/SpringArm3D");

        if (!TargetPath.IsEmpty) _target = GetNodeOrNull<Node3D>(TargetPath);

        _arm.CollisionMask = CollisionPullIn ? Layers.World : 0;
        _arm.RotationDegrees = _arm.RotationDegrees with { X = -PitchDegrees };
        _targetZoom = Mathf.Clamp(_arm.SpringLength, MinZoom, MaxZoom);
        _arm.SpringLength = _targetZoom;

        // Snap to the target on the first frame instead of gliding in from the origin.
        if (_target is not null) GlobalPosition = _target.GlobalPosition;

        _instance = this;
    }

    public override void _ExitTree()
    {
        if (_instance == this) _instance = null;
    }

    public void SetTarget(Node3D target) => _target = target;

    public float YawDegrees => _yaw.RotationDegrees.Y;

    /// <summary>
    /// Kicks the camera briefly. Reserved for hits that matter — a shake on every swing
    /// stops meaning anything and just makes the game hard to look at.
    /// </summary>
    public static void Shake(double seconds = 0.18, float scale = 1f)
    {
        if (_instance is null) return;

        // Never shorten an ongoing shake; overlapping impacts should not cut each other off.
        _instance._shakeDuration = System.Math.Max(_instance._shakeDuration, seconds);
        _instance._shake = System.Math.Max(_instance._shake, seconds * scale);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.CameraZoomIn)) _targetZoom -= ZoomStep;
        else if (@event.IsActionPressed(GameActions.CameraZoomOut)) _targetZoom += ZoomStep;
        else if (@event.IsActionPressed(GameActions.CameraDrag)) _dragging = true;
        else if (@event.IsActionReleased(GameActions.CameraDrag)) _dragging = false;

        _targetZoom = Mathf.Clamp(_targetZoom, MinZoom, MaxZoom);

        if (_dragging && @event is InputEventMouseMotion motion)
        {
            _yaw.RotationDegrees = _yaw.RotationDegrees with
            {
                Y = _yaw.RotationDegrees.Y - (motion.Relative.X * DragSensitivity),
            };
        }
    }

    public override void _Process(double delta)
    {
        var rotate = Godot.Input.GetAxis(GameActions.CameraRotateRight, GameActions.CameraRotateLeft);
        if (rotate != 0)
        {
            _yaw.RotationDegrees = _yaw.RotationDegrees with
            {
                Y = _yaw.RotationDegrees.Y + (rotate * RotateSpeed * (float)delta),
            };
        }

        // 1 - e^(-k*dt) keeps the smoothing identical at 30 and 240 fps.
        if (_target is not null)
        {
            var t = 1f - Mathf.Exp(-FollowSharpness * (float)delta);
            GlobalPosition = GlobalPosition.Lerp(_target.GlobalPosition, t) + ShakeOffset(delta);
        }

        var zt = 1f - Mathf.Exp(-ZoomSharpness * (float)delta);
        _arm.SpringLength = Mathf.Lerp(_arm.SpringLength, _targetZoom, zt);
    }

    /// <summary>Displacement for this frame's shake, decaying to nothing.</summary>
    private Vector3 ShakeOffset(double delta)
    {
        if (_shake <= 0) return Vector3.Zero;

        _shake -= delta;

        if (_shake <= 0)
        {
            _shakeDuration = 0;
            return Vector3.Zero;
        }

        // Decay with the square so it hits hard and settles fast rather than wobbling.
        var falloff = (float)(_shake / System.Math.Max(_shakeDuration, 0.0001));
        var amount = ShakeStrength * falloff * falloff;

        // Horizontal only: vertical shake on a fixed-pitch camera reads as the ground moving.
        return new Vector3(
            (float)GD.RandRange(-amount, amount),
            0,
            (float)GD.RandRange(-amount, amount));
    }
}
