using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// The shield dome shown while Guard Stance is held.
/// <para>
/// A HUD label is not enough: during a fight the player is watching the character and the
/// telegraph, not the corner of the screen. The state has to be visible where the eye
/// already is. It also flashes on every hit it absorbs, which is what makes guarding feel
/// like it did something rather than like nothing happened.
/// </para>
/// </summary>
public partial class GuardVisual : Node3D
{
    private MeshInstance3D? _dome;
    private StandardMaterial3D? _material;

    private double _pulse;
    private double _flash;
    private float _scale;
    private bool _active;

    [Export] public Color Color { get; set; } = new(0.45f, 0.75f, 1f);
    [Export] public float Radius { get; set; } = 1.05f;

    /// <summary>Resting opacity. Low: it must read clearly without hiding the fight.</summary>
    [Export] public float BaseAlpha { get; set; } = 0.16f;

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            AlbedoColor = Color with { A = 0f },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // Both faces: the player stands inside the dome and must still see the far side.
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        _dome = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = Radius, Height = Radius * 2f, RadialSegments = 24, Rings = 12 },
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.95f, 0),
        };

        AddChild(_dome);
        Visible = false;
    }

    public void SetGuarding(bool guarding)
    {
        // Parenting is deferred, so this can be reached before _Ready has built the mesh.
        if (_dome is null) return;

        _active = guarding;

        if (guarding)
        {
            Visible = true;
            _scale = 0.55f; // pops outward on activation, so the moment of pressing is felt
            _flash = 0.12;
        }
    }

    /// <summary>Called when the guard absorbs a hit.</summary>
    public void Flash()
    {
        if (_dome is null) return;
        _flash = 0.18;
    }

    public override void _Process(double delta)
    {
        if (!Visible || _dome is null || _material is null) return;

        _pulse += delta * 3.0;
        if (_flash > 0) _flash -= delta;

        // Grow toward full size when active, shrink away when released.
        var target = _active ? 1f : 0f;
        _scale = Mathf.MoveToward(_scale, target, (float)delta * 6f);

        if (!_active && _scale <= 0.01f)
        {
            Visible = false;
            _material.AlbedoColor = Color with { A = 0f };
            return;
        }

        _dome.Scale = Vector3.One * _scale;

        var pulse = BaseAlpha + (0.05f * (float)Mathf.Sin(_pulse));
        var flash = _flash > 0 ? 0.45f * (float)(_flash / 0.18) : 0f;

        _material.AlbedoColor = Color with { A = (pulse + flash) * _scale };
    }
}
