using Godot;
using Kiln.Game.Input;

namespace Kiln.Game.Camera;

/// <summary>
/// Fixed-pitch semi-isometric follow camera (D2, FR-1.6).
/// <para>
/// Node layout: CameraRig → Yaw → SpringArm3D → Camera3D. The SpringArm gives collision
/// pull-in for free, and keeping yaw on its own node means camera-relative WASD input is
/// just the yaw basis.
/// </para>
/// </summary>
public partial class CameraRig : Node3D
{
    private Node3D _yaw = null!;
    private SpringArm3D _arm = null!;
    private Node3D? _target;

    private float _targetZoom;
    private bool _dragging;

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

    public override void _Ready()
    {
        _yaw = GetNode<Node3D>("Yaw");
        _arm = GetNode<SpringArm3D>("Yaw/SpringArm3D");

        if (!TargetPath.IsEmpty) _target = GetNodeOrNull<Node3D>(TargetPath);

        _arm.RotationDegrees = _arm.RotationDegrees with { X = -PitchDegrees };
        _targetZoom = Mathf.Clamp(_arm.SpringLength, MinZoom, MaxZoom);
        _arm.SpringLength = _targetZoom;

        // Snap to the target on the first frame instead of gliding in from the origin.
        if (_target is not null) GlobalPosition = _target.GlobalPosition;
    }

    public void SetTarget(Node3D target) => _target = target;

    public float YawDegrees => _yaw.RotationDegrees.Y;

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
            GlobalPosition = GlobalPosition.Lerp(_target.GlobalPosition, t);
        }

        var zt = 1f - Mathf.Exp(-ZoomSharpness * (float)delta);
        _arm.SpringLength = Mathf.Lerp(_arm.SpringLength, _targetZoom, zt);
    }
}
