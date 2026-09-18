using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.Combat;

/// <summary>
/// A shape floating above an enemy identifying its combat role.
/// <para>
/// The tactical layer only works if the player can tell a Mender from a Bruiser at a
/// glance — "kill the healer first" is not a decision anyone can make about identical
/// capsules. Shape <b>and</b> colour both carry the meaning, so it stays readable for
/// colourblind players (NFR-A.1) and survives the placeholder art being replaced.
/// </para>
/// </summary>
public partial class RoleMarker : Node3D
{
    private MeshInstance3D? _mesh;
    private double _spin;

    [Export] public Vector3 Offset { get; set; } = new(0, 2.45f, 0);

    /// <summary>Set before the node enters the tree; the marker builds itself on ready.</summary>
    public EnemyRole Role { get; set; } = EnemyRole.Bruiser;

    public override void _Ready() => Build(Role);

    /// <summary>Role colours. Deliberately far apart in hue, not shades of one family tint.</summary>
    public static Color ColorFor(EnemyRole role) => role switch
    {
        EnemyRole.Archer => new Color(0.95f, 0.78f, 0.25f),
        EnemyRole.Shielder => new Color(0.40f, 0.62f, 1.00f),
        EnemyRole.Mender => new Color(0.35f, 0.90f, 0.45f),
        EnemyRole.Bomber => new Color(1.00f, 0.35f, 0.25f),
        _ => new Color(0.80f, 0.80f, 0.84f),
    };

    private void Build(EnemyRole role)
    {
        _mesh?.QueueFree();

        var color = ColorFor(role);

        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 0.5f,
            NoDepthTest = true,
            RenderPriority = 6,
        };

        _mesh = new MeshInstance3D
        {
            Mesh = MeshFor(role),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        AddChild(_mesh);
        TopLevel = true;

        // The Bruiser is the baseline and deliberately unmarked: marking everything is the
        // same as marking nothing.
        Visible = role != EnemyRole.Bruiser;
    }

    private static Mesh MeshFor(EnemyRole role) => role switch
    {
        // Pointed, like an arrowhead.
        EnemyRole.Archer => new CylinderMesh { TopRadius = 0f, BottomRadius = 0.22f, Height = 0.36f, RadialSegments = 4 },

        // Blocky, like a shield.
        EnemyRole.Shielder => new BoxMesh { Size = new Vector3(0.34f, 0.34f, 0.08f) },

        // A cross.
        EnemyRole.Mender => BuildCross(),

        // Round, like a bomb.
        EnemyRole.Bomber => new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 10, Rings = 6 },

        _ => new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 8, Rings = 4 },
    };

    private static ArrayMesh BuildCross()
    {
        var mesh = new ArrayMesh();
        AppendBox(mesh, new Vector3(0.36f, 0.12f, 0.08f));
        AppendBox(mesh, new Vector3(0.12f, 0.36f, 0.08f));
        return mesh;
    }

    private static void AppendBox(ArrayMesh target, Vector3 size)
    {
        var box = new BoxMesh { Size = size };
        target.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, box.SurfaceGetArrays(0));
    }

    public override void _Process(double delta)
    {
        if (!Visible || _mesh is null) return;

        var parent = GetParent<Node3D>();
        var camera = GetViewport().GetCamera3D();

        if (parent is null || camera is null) return;

        GlobalPosition = parent.GlobalPosition + Offset;

        // Face the camera, and turn slowly so it reads as a marker rather than scenery.
        _spin += delta * 1.2;
        GlobalBasis = camera.GlobalBasis.Orthonormalized().Rotated(camera.GlobalBasis.Z.Normalized(), (float)_spin * 0.15f);
    }
}
