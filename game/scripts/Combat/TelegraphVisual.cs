using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.Combat;

/// <summary>
/// The ground decal warning of an incoming attack (CBT-08, FR-3.2).
/// <para>
/// This is the single most important piece of readability in the game. Under click-to-move
/// the player has no dodge, so surviving means seeing the shape, judging the timer and
/// walking out — which only works if the extent and the remaining time are both obvious at
/// a glance. The outline shows where; the filling interior shows when.
/// </para>
/// </summary>
public partial class TelegraphVisual : Node3D
{
    private MeshInstance3D _fill = null!;
    private MeshInstance3D _edge = null!;
    private StandardMaterial3D _fillMaterial = null!;
    private StandardMaterial3D _edgeMaterial = null!;

    private double _duration;
    private double _elapsed;
    private float _radius;
    private float _angle;
    private bool _running;

    [Export] public Color Color { get; set; } = new(1f, 0.35f, 0.28f);

    public override void _Ready()
    {
        _fillMaterial = Material(0.30f);
        _edgeMaterial = Material(0.65f);

        _fill = new MeshInstance3D { MaterialOverride = _fillMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _edge = new MeshInstance3D { MaterialOverride = _edgeMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };

        AddChild(_edge);
        AddChild(_fill);

        TopLevel = true;
        Visible = false;
    }

    private StandardMaterial3D Material(float alpha) => new()
    {
        AlbedoColor = Color with { A = alpha },
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = true,
        RenderPriority = 2,
    };

    /// <summary>Starts a telegraph. <paramref name="duration"/> is the wind-up remaining.</summary>
    public void Begin(TelegraphShape shape, Vector3 origin, Vector3 forward, float radius, float angleDegrees, double duration)
    {
        _radius = radius;
        _angle = shape == TelegraphShape.Cone ? angleDegrees : 360f;
        _duration = System.Math.Max(0.05, duration);
        _elapsed = 0;
        _running = true;

        var fan = AoeGeometry.Fan(_radius, _angle);
        _edge.Mesh = fan;
        _fill.Mesh = fan;

        GlobalPosition = origin + (Vector3.Up * 0.05f);

        var flat = forward with { Y = 0 };
        Rotation = flat.LengthSquared() > 0.0001f
            ? new Vector3(0, Mathf.Atan2(flat.X, flat.Z), 0)
            : Vector3.Zero;

        // The outline sits at full size from the first frame: the player must know the
        // extent immediately, not watch it grow into place.
        _edge.Scale = Vector3.One;
        _fill.Scale = new Vector3(0.001f, 1, 0.001f);

        Visible = true;
    }

    public void Cancel()
    {
        _running = false;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_running) return;

        _elapsed += delta;
        var t = (float)Mathf.Clamp(_elapsed / _duration, 0, 1);

        _fill.Scale = new Vector3(Mathf.Max(t, 0.001f), 1, Mathf.Max(t, 0.001f));

        // Brighten as it fills, so the last moments are unmistakable even in peripheral vision.
        _edgeMaterial.AlbedoColor = Color with { A = 0.55f + (0.35f * t) };
        _fillMaterial.AlbedoColor = Color with { A = 0.22f + (0.25f * t) };

        if (_elapsed >= _duration) Cancel();
    }
}

/// <summary>Shared fan geometry for telegraphs and area effects.</summary>
public static class AoeGeometry
{
    /// <summary>A flat fan on the XZ plane, centred on the origin, opening along +Z.</summary>
    public static ArrayMesh Fan(float radius, float angleDegrees)
    {
        const int segmentsPerTurn = 48;

        var segments = Mathf.Max(3, Mathf.RoundToInt(segmentsPerTurn * (angleDegrees / 360f)));
        var half = Mathf.DegToRad(angleDegrees * 0.5f);

        var vertices = new System.Collections.Generic.List<Vector3> { Vector3.Zero };

        for (var i = 0; i <= segments; i++)
        {
            var t = segments == 0 ? 0f : i / (float)segments;
            var a = -half + (t * half * 2f);
            vertices.Add(new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius));
        }

        var indices = new System.Collections.Generic.List<int>();
        for (var i = 1; i < vertices.Count - 1; i++)
        {
            indices.Add(0);
            indices.Add(i + 1);
            indices.Add(i);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
