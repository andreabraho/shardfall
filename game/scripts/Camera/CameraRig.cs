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
    private Vector2 _restoreCursorTo;
    private Vector3 _kick;
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

    /// <summary>Degrees per pixel of look-drag.</summary>
    [Export] public float DragSensitivity { get; set; } = 0.35f;

    /// <summary>Peak camera displacement from an impact, in metres. Deliberately subtle.</summary>
    [Export] public float ShakeStrength { get; set; } = 0.10f;

    /// <summary>How fast the camera returns after a kick. Higher settles sooner.</summary>
    [Export] public float KickRecovery { get; set; } = 9f;

    /// <summary>Off disables camera impacts entirely. Belongs in the settings menu (NFR-A.1).</summary>
    [Export] public bool EnableScreenShake { get; set; } = true;

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
    /// Nudges the camera away from an impact, then eases back.
    /// <para>
    /// A directional kick rather than random per-frame jitter. Jitter reads as the screen
    /// trembling — it is uncomfortable to look at and says nothing about what happened,
    /// while a single push in the direction of the blow reads as impact and settles.
    /// </para>
    /// </summary>
    /// <param name="fromDirection">World direction the hit came from. Zero picks a stable fallback.</param>
    /// <param name="strength">0..1, normally the fraction of health lost.</param>
    public static void Kick(Vector3 fromDirection, float strength)
    {
        if (_instance is null || !_instance.EnableScreenShake) return;

        // Small hits do not move the camera at all. Reacting to every scratch is what turns
        // feedback into noise.
        if (strength < MinKickStrength) return;

        var flat = fromDirection with { Y = 0 };
        var direction = flat.LengthSquared() > 0.0001f ? flat.Normalized() : Vector3.Forward;

        var impulse = direction * _instance.ShakeStrength * Mathf.Min(strength / 0.25f, 1.5f);

        // Add rather than replace, so several blows at once land as one firmer push.
        _instance._kick += impulse;

        if (_instance._kick.Length() > _instance.ShakeStrength * 2f)
        {
            _instance._kick = _instance._kick.Normalized() * _instance.ShakeStrength * 2f;
        }
    }

    /// <summary>Fraction of health lost below which the camera stays still.</summary>
    private const float MinKickStrength = 0.04f;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.CameraZoomIn)) _targetZoom -= ZoomStep;
        else if (@event.IsActionPressed(GameActions.CameraZoomOut)) _targetZoom += ZoomStep;
        else if (@event.IsActionPressed(GameActions.CameraDrag)) Look(true);
        else if (@event.IsActionReleased(GameActions.CameraDrag)) Look(false);

        _targetZoom = Mathf.Clamp(_targetZoom, MinZoom, MaxZoom);

        if (_dragging && @event is InputEventMouseMotion motion)
        {
            _yaw.RotationDegrees = _yaw.RotationDegrees with
            {
                Y = _yaw.RotationDegrees.Y - (motion.Relative.X * DragSensitivity),
            };
        }
    }

    /// <summary>
    /// Starts or stops looking around, capturing the cursor while it lasts (MOV-10).
    /// </summary>
    /// <remarks>
    /// Captured rather than merely hidden: a swing of the camera is easily a whole screen
    /// width of mouse travel, and without capture the pointer runs off the window and the
    /// turn stops halfway. On release it goes back exactly where it was taken from, so a
    /// look does not cost the player the cursor position they had lined up.
    /// </remarks>
    private void Look(bool on)
    {
        if (_dragging == on) return;

        if (on && UI.UiState.ModalOpen) return;

        _dragging = on;

        if (on) _restoreCursorTo = GetViewport().GetMousePosition();

        Godot.Input.MouseMode = on ? Godot.Input.MouseModeEnum.Captured : Godot.Input.MouseModeEnum.Visible;

        if (!on) Godot.Input.WarpMouse(_restoreCursorTo);
    }

    public override void _Process(double delta)
    {
        // The button can be released while the window is not focused, or a panel can open
        // under the held button, and either way the release event never arrives. Letting go
        // of the cursor is not something to get wrong: a captured mouse with nothing driving
        // it looks exactly like a frozen game.
        if (_dragging && (!Godot.Input.IsActionPressed(GameActions.CameraDrag) || UI.UiState.ModalOpen))
        {
            Look(false);
        }

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

    /// <summary>Current impact displacement, easing smoothly back to zero.</summary>
    private Vector3 ShakeOffset(double delta)
    {
        if (_kick.LengthSquared() < 0.000001f)
        {
            _kick = Vector3.Zero;
            return Vector3.Zero;
        }

        var offset = _kick;

        // Frame-rate independent ease-out, matching the follow smoothing.
        _kick = _kick.Lerp(Vector3.Zero, 1f - Mathf.Exp(-KickRecovery * (float)delta));

        return offset;
    }
}
