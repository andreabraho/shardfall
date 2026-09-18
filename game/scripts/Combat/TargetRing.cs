using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// A ring on the ground under the current attack target.
/// <para>
/// Under click-to-move the player commits to a target rather than aiming each swing, so
/// which enemy is selected has to be unambiguous — especially in a crowd, where two bodies
/// close together are otherwise impossible to tell apart.
/// </para>
/// </summary>
public partial class TargetRing : Node3D
{
    private MeshInstance3D _ring = null!;
    private StandardMaterial3D _material = null!;
    private Node3D? _follow;
    private double _pulse;

    [Export] public Color Color { get; set; } = new(1f, 0.55f, 0.35f);
    [Export] public float Radius { get; set; } = 0.95f;

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            AlbedoColor = Color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 4,
        };

        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = Radius * 0.88f, OuterRadius = Radius, RingSegments = 28 },
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(90, 0, 0),
        };

        AddChild(_ring);
        TopLevel = true;
        Visible = false;
    }

    public void Follow(Node3D? body)
    {
        _follow = body;
        Visible = body is not null;
    }

    public override void _Process(double delta)
    {
        if (_follow is null || !IsInstanceValid(_follow))
        {
            Visible = false;
            _follow = null;
            return;
        }

        GlobalPosition = _follow.GlobalPosition + (Vector3.Up * 0.06f);

        // A slow pulse keeps it readable against busy ground without being distracting.
        _pulse += delta * 3.0;
        _material.AlbedoColor = Color with { A = 0.55f + (0.35f * (float)Mathf.Sin(_pulse)) };
    }
}
