using System.Collections.Generic;
using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// Shows the shape an area attack covered — an expanding ring for circles, a wedge for
/// cones.
/// <para>
/// Not decoration: with placeholder art and no animation, this is the only way the player
/// can tell what an area attack actually reached. The same shapes are reused for enemy
/// telegraphs in CBT-08, where being able to read the area is the whole mechanic.
/// </para>
/// </summary>
public partial class AoeVisual : Node3D
{
    private static AoeVisual? _instance;

    private sealed class Flash
    {
        public required MeshInstance3D Mesh { get; init; }
        public required StandardMaterial3D Material { get; init; }
        public required Color Color { get; init; }
        public required float Radius { get; init; }
        public double Elapsed { get; set; }
    }

    private readonly List<Flash> _active = [];
    private readonly List<MeshInstance3D> _pool = [];

    [Export] public double Duration { get; set; } = 0.35;
    [Export] public Color FriendlyColor { get; set; } = new(0.55f, 0.85f, 1f);
    [Export] public Color HostileColor { get; set; } = new(1f, 0.4f, 0.3f);

    private MeshInstance3D? _preview;
    private StandardMaterial3D? _previewMaterial;
    private double _previewPulse;

    public override void _Ready() => _instance = this;

    public override void _ExitTree()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// Shows a persistent aiming circle while a ground-targeted skill is being aimed.
    /// <para>
    /// Without this the player casts and only then learns where it landed, which is exactly
    /// the confusion that prompted it — "does it go around me?". The area has to be visible
    /// before committing, not after.
    /// </para>
    /// </summary>
    public static void ShowPreview(Vector3 center, float radius) => _instance?.UpdatePreview(center, radius);

    public static void HidePreview()
    {
        if (_instance?._preview is not null) _instance._preview.Visible = false;
    }

    private void UpdatePreview(Vector3 center, float radius)
    {
        if (_preview is null)
        {
            _previewMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true,
                RenderPriority = 2,
            };

            _preview = new MeshInstance3D
            {
                MaterialOverride = _previewMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                TopLevel = true,
            };

            AddChild(_preview);
        }

        _preview.Mesh = BuildFan(radius, 360f);
        _preview.GlobalPosition = center + (Vector3.Up * 0.06f);
        _preview.Visible = true;

        _previewPulse += GetProcessDeltaTime() * 4.0;
        _previewMaterial!.AlbedoColor = FriendlyColor with
        {
            A = 0.20f + (0.10f * (float)Mathf.Sin(_previewPulse)),
        };
    }

    /// <summary>A filled circle centred on a point.</summary>
    public static void Circle(Vector3 center, float radius, bool hostile = false)
        => _instance?.Spawn(center, radius, Vector3.Zero, 360f, hostile);

    /// <summary>A wedge opening along <paramref name="forward"/>.</summary>
    public static void Cone(Vector3 origin, Vector3 forward, float radius, float angleDegrees, bool hostile = false)
        => _instance?.Spawn(origin, radius, forward, angleDegrees, hostile);

    private void Spawn(Vector3 origin, float radius, Vector3 forward, float angleDegrees, bool hostile)
    {
        var mesh = Rent();

        mesh.Mesh = BuildFan(radius, angleDegrees);
        mesh.GlobalPosition = origin + (Vector3.Up * 0.08f);

        // A 360° fan needs no orientation; a wedge is rotated to face the attack direction.
        if (angleDegrees < 359f && (forward with { Y = 0 }).LengthSquared() > 0.0001f)
        {
            var flat = (forward with { Y = 0 }).Normalized();
            mesh.Rotation = new Vector3(0, Mathf.Atan2(flat.X, flat.Z), 0);
        }
        else
        {
            mesh.Rotation = Vector3.Zero;
        }

        var color = hostile ? HostileColor : FriendlyColor;
        var material = (StandardMaterial3D)mesh.MaterialOverride;
        material.AlbedoColor = color;

        mesh.Visible = true;
        _active.Add(new Flash { Mesh = mesh, Material = material, Color = color, Radius = radius });
    }

    /// <summary>Builds a flat fan on the XZ plane, centred on the origin.</summary>
    private static ArrayMesh BuildFan(float radius, float angleDegrees)
    {
        const int segmentsPerTurn = 48;

        var segments = Mathf.Max(3, Mathf.RoundToInt(segmentsPerTurn * (angleDegrees / 360f)));
        var half = Mathf.DegToRad(angleDegrees * 0.5f);

        var vertices = new List<Vector3> { Vector3.Zero };

        for (var i = 0; i <= segments; i++)
        {
            var t = segments == 0 ? 0f : i / (float)segments;
            var a = -half + (t * half * 2f);
            vertices.Add(new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius));
        }

        var indices = new List<int>();
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

    private MeshInstance3D Rent()
    {
        if (_pool.Count > 0)
        {
            var reused = _pool[^1];
            _pool.RemoveAt(_pool.Count - 1);
            return reused;
        }

        var instance = new MeshInstance3D
        {
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true,
                RenderPriority = 3,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
        };

        AddChild(instance);
        return instance;
    }

    public override void _Process(double delta)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var flash = _active[i];
            flash.Elapsed += delta;

            if (flash.Elapsed >= Duration)
            {
                flash.Mesh.Visible = false;
                _pool.Add(flash.Mesh);
                _active.RemoveAt(i);
                continue;
            }

            var t = (float)(flash.Elapsed / Duration);

            // Snap to full size immediately, then fade: the player needs to see the extent
            // at the moment of the hit, not watch it grow afterwards.
            var scale = Mathf.Lerp(0.85f, 1.05f, t);
            flash.Mesh.Scale = new Vector3(scale, 1, scale);
            flash.Material.AlbedoColor = flash.Color with { A = 0.45f * (1f - t) };
        }
    }
}
