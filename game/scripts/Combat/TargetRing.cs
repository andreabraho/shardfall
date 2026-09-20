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

    /// <summary>Used only for a body that does not say how big it is.</summary>
    [Export] public float Radius { get; set; } = 0.6f;

    /// <summary>How far outside the creature the ring sits, as a multiple of its own width.</summary>
    /// <remarks>
    /// Just outside rather than generously around: the ring has to say "this one" in a
    /// crowd, and a ring wide enough to enclose its neighbours says the opposite.
    /// </remarks>
    [Export] public float Margin { get; set; } = 1.35f;

    /// <summary>
    /// Width of the drawn band, in metres — constant whatever the creature's size.
    /// </summary>
    /// <remarks>
    /// Not a fraction of the radius. Scaling the line with the ring would make a boss's
    /// marker a thick smear and a rat's a hairline, when both need to be equally easy to
    /// pick out of the ground clutter.
    /// </remarks>
    [Export] public float Thickness { get; set; } = 0.09f;

    private float _drawnFor = -1f;

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

        // Unrotated. A TorusMesh already lies in the XZ plane with its hole along Y, so it is
        // flat on the ground as built; the quarter turn it used to carry stood it upright
        // like a wheel, and an upright ring facing the camera reads as a circle drawn over
        // the enemy rather than as a mark on the ground beneath it.
        _ring = new MeshInstance3D
        {
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        AddChild(_ring);
        Resize(Radius);

        TopLevel = true;
        Visible = false;
    }

    public void Follow(Node3D? body)
    {
        _follow = body;
        Visible = body is not null;

        if (body is not null) Resize(RadiusFor(body));
    }

    /// <summary>
    /// How big the ring should be for this body.
    /// </summary>
    /// <remarks>
    /// Asked of the creature rather than fixed, because one size cannot fit a rat and a
    /// boss: the ring was drawn at nearly a metre for everything, which around a wolf half
    /// that wide read as a circle on the floor near it rather than a mark on it.
    /// </remarks>
    private float RadiusFor(Node3D body)
    {
        var visual = body.GetNodeOrNull<Visual.VisualRoot>("VisualRoot");

        return (visual?.GroundRadius ?? Radius) * Margin;
    }

    private void Resize(float radius)
    {
        // Rebuilt only when the size actually changes: re-marking the same creature, which
        // every swing does, would otherwise throw away a mesh and build another every time.
        if (Mathf.IsEqualApprox(radius, _drawnFor)) return;

        _drawnFor = radius;

        _ring.Mesh = new TorusMesh
        {
            InnerRadius = Mathf.Max(radius - Thickness, 0.02f),
            OuterRadius = Mathf.Max(radius, 0.04f),
            RingSegments = 28,
        };
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
