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

    [Export] public Color Color { get; set; } = new(0.62f, 0.06f, 0.05f);

    /// <summary>Metres across the outline band.</summary>
    private const float OutlineWidth = 0.07f;

    /// <summary>
    /// The high-contrast warning (UIX-04): a yellow no creature, grass or floor in the game is,
    /// a band three times as thick, and stripes in the fill, so the shape says "danger" to
    /// anyone who cannot tell the red from the ground.
    /// </summary>
    private static readonly Color Contrast = new(1f, 0.86f, 0.1f);

    private const float ContrastOutlineWidth = 0.2f;

    private static ImageTexture? _stripes;

    private bool _contrast;

    /// <summary>Diagonal stripes, laid on the ground in world space so they never swim as the fill grows.</summary>
    private static ImageTexture Stripes()
    {
        if (_stripes is not null) return _stripes;

        const int size = 64;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // Four stripes across the tile, half of each band solid.
                var on = ((x + y) % (size / 4)) < size / 8;
                image.SetPixel(x, y, on ? Colors.White : new Color(1, 1, 1, 0.25f));
            }
        }

        return _stripes = ImageTexture.CreateFromImage(image);
    }

    public override void _Ready()
    {
        _fillMaterial = Material(0.12f);
        _edgeMaterial = Material(0.12f);

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
        // Still drawn over everything, so a raised road or floor never hides a warning. What
        // made it a wall of red was the opacity, not this (2026-09-24).
        NoDepthTest = true,
        RenderPriority = 2,
    };

    /// <summary>Starts a telegraph. <paramref name="duration"/> is the wind-up remaining.</summary>
    public void Begin(TelegraphShape shape, Vector3 origin, Vector3 forward, float radius, float angleDegrees, double duration)
    {
        _radius = radius;
        _angle = shape == TelegraphShape.Cone ? angleDegrees : 360f;

        // Read now, so a setting changed mid-fight takes effect at the next warning.
        _contrast = Settings.GameSettings.HighContrastWarnings;

        _fillMaterial.AlbedoTexture = _contrast ? Stripes() : null;
        _fillMaterial.Uv1Triplanar = _contrast;
        _fillMaterial.Uv1WorldTriplanar = _contrast;
        _fillMaterial.Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f);
        _duration = System.Math.Max(0.05, duration);
        _elapsed = 0;
        _running = true;

        // A thin outline for the extent, and a faint fill growing to meet it for the timing.
        // The outline used to be the whole shape at two-thirds opacity.
        _edge.Mesh = AoeGeometry.Outline(_radius, _angle, _contrast ? ContrastOutlineWidth : OutlineWidth);
        _fill.Mesh = AoeGeometry.Fan(_radius, _angle);
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
        // Outline and fill at the same transparency (2026-09-24): the edge reads by being a
        // line, not by being brighter.
        if (_contrast)
        {
            // Bold from the first frame: the edge is the part that must never be missed.
            _edgeMaterial.AlbedoColor = Contrast with { A = 0.55f + (0.35f * t) };
            _fillMaterial.AlbedoColor = Contrast with { A = 0.18f + (0.3f * t) };
        }
        else
        {
            _edgeMaterial.AlbedoColor = Color with { A = 0.08f + (0.2f * t) };
            _fillMaterial.AlbedoColor = Color with { A = 0.08f + (0.2f * t) };
        }

        if (_elapsed >= _duration) Cancel();
    }
}

/// <summary>Shared fan geometry for telegraphs and area effects.</summary>
public static class AoeGeometry
{
    /// <summary>
    /// The edge of a fan as a band <paramref name="width"/> wide: the arc, and for anything
    /// short of a full circle the two straight sides.
    /// </summary>
    public static ArrayMesh Outline(float radius, float angleDegrees, float width)
    {
        const int segmentsPerTurn = 64;
        var segments = Mathf.Max(3, Mathf.RoundToInt(segmentsPerTurn * (angleDegrees / 360f)));
        var half = Mathf.DegToRad(angleDegrees * 0.5f);
        var inner = Mathf.Max(0.01f, radius - width);

        var vertices = new System.Collections.Generic.List<Vector3>();
        var indices = new System.Collections.Generic.List<int>();

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var i = vertices.Count;
            vertices.AddRange([a, b, c, d]);
            indices.AddRange([i, i + 1, i + 2, i, i + 2, i + 3]);
        }

        Vector3 At(float r, float a) => new(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);

        for (var i = 0; i < segments; i++)
        {
            var a0 = -half + (i / (float)segments * half * 2f);
            var a1 = -half + ((i + 1) / (float)segments * half * 2f);
            Quad(At(inner, a0), At(radius, a0), At(radius, a1), At(inner, a1));
        }

        if (angleDegrees < 359f)
        {
            foreach (var a in new[] { -half, half })
            {
                var along = At(1f, a);
                var side = new Vector3(along.Z, 0, -along.X) * (width * 0.5f);
                Quad(-side, side, (along * radius) + side, (along * radius) - side);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        // Facing up, so a pattern laid on it from above lies flat (UIX-04's stripes).
        arrays[(int)Mesh.ArrayType.Normal] = System.Linq.Enumerable.Repeat(Vector3.Up, vertices.Count).ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

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

        // Facing up, so a pattern laid on it from above lies flat (UIX-04's stripes).
        arrays[(int)Mesh.ArrayType.Normal] = System.Linq.Enumerable.Repeat(Vector3.Up, vertices.Count).ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
