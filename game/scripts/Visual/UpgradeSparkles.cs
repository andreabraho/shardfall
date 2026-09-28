using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// What a +8 or +9 piece gives off (2026-09-28, at your call): diamond glints, white and pink,
/// winking on and off close round the blade or the body, with a fine white dust among them.
/// It replaces the coloured shell and the light that lit the whole character.
/// </summary>
/// <remarks>
/// A node on the blade, or on the model for armour, so taking it off is taking the node away.
/// The glints live in that node's own space, so they stay on the sword through a swing and on
/// the body as it runs.
/// </remarks>
public partial class UpgradeSparkles : Node3D
{
    private const string NodeName = "UpgradeSparkles";

    /// <summary>How far round the blade the glints are born, in the blade's own units.</summary>
    private const float BladeMargin = 0.05f;

    private static readonly Color White = new(1f, 0.98f, 1f);
    private static readonly Color Pink = new(1f, 0.62f, 0.86f);

    /// <summary>Glints on a blade for its upgrade level: none below +8, fewer at +8 than at +9.</summary>
    public static void OnBlade(MeshInstance3D? blade, int upgrade)
    {
        if (blade is null) return;

        Remove(blade);

        if (upgrade < 8 || blade.Mesh is null) return;

        var box = blade.Mesh.GetAabb();
        var half = box.Size * 0.5f;
        var extents = new Vector3(half.X + BladeMargin, half.Y + BladeMargin, half.Z + BladeMargin);

        var node = new UpgradeSparkles { Name = NodeName, Position = box.GetCenter() };
        blade.AddChild(node);
        node.Dress(extents, Shrink(blade), upgrade >= 9 ? 1f : 0.45f, glints: 36, dust: 64, size: 0.34f);
    }

    /// <summary>
    /// Glints round the body for the armour's upgrade level: the chest and shoulders, where the
    /// armour is, not the feet.
    /// </summary>
    public static void OnBody(Node3D? model, IEnumerable<MeshInstance3D> body, int upgrade)
    {
        if (model is null) return;

        Remove(model);

        if (upgrade < 8) return;

        var meshes = body.Where(m => GodotObject.IsInstanceValid(m) && m.Mesh is not null).ToList();

        if (meshes.Count == 0) return;

        // The body's bounds in the model's own space.
        var bounds = meshes
            .Select(m => (model.GlobalTransform.AffineInverse() * m.GlobalTransform) * m.Mesh!.GetAabb())
            .Aggregate((a, b) => a.Merge(b));

        var size = bounds.Size;
        var extents = new Vector3(size.X * 0.34f, size.Y * 0.2f, size.Z * 0.34f);
        var centre = bounds.Position + new Vector3(size.X * 0.5f, size.Y * 0.42f, size.Z * 0.5f);

        var node = new UpgradeSparkles { Name = NodeName, Position = centre };
        model.AddChild(node);
        node.Dress(extents, Shrink(model), upgrade >= 9 ? 1f : 0.45f, glints: 40, dust: 70, size: 0.3f);
    }

    private static void Remove(Node owner)
    {
        if (owner.GetNodeOrNull<UpgradeSparkles>(NodeName) is not { } old) return;

        owner.RemoveChild(old);
        old.QueueFree();
    }

    /// <summary>What undoes the owner's scale on a glint's size, so a glint is the same size on any model.</summary>
    private static float Shrink(Node3D owner)
    {
        var scale = owner.GlobalTransform.Basis.Scale;

        return 1f / Mathf.Max(0.01f, (scale.X + scale.Y + scale.Z) / 3f);
    }

    private void Dress(Vector3 extents, float shrink, float amount, int glints, int dust, float size)
    {
        if (Load("star_06.png") is { } star)
        {
            AddChild(Layer("Glints", Mathf.Max(3, Mathf.RoundToInt(glints * amount)), 1.1, extents, star, size * shrink, spin: 40f));
        }

        if (Load("circle_05.png") is { } point)
        {
            AddChild(Layer("Dust", Mathf.Max(4, Mathf.RoundToInt(dust * amount)), 0.7, extents, point, size * 0.3f * shrink, spin: 0f));
        }
    }

    private static GpuParticles3D Layer(string name, int count, double lifetime, Vector3 extents, Texture2D texture, float size, float spin)
    {
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = extents,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 0f,
            InitialVelocityMax = 0.04f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.55f,
            ScaleMax = 1.25f,
            ScaleCurve = Wink(),

            // Each glint white or pink, or between, picked at birth.
            ColorInitialRamp = new GradientTexture1D { Gradient = Between(White, Pink) },
            AngleMin = -30f,
            AngleMax = 30f,
            AngularVelocityMin = -spin,
            AngularVelocityMax = spin,
        };

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = texture,
        };

        return new GpuParticles3D
        {
            Name = name,
            Amount = count,
            Lifetime = lifetime,
            Randomness = 0.5f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(size, size), Material = material },
            LocalCoords = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-3, -3, -3), new Vector3(6, 6, 6)),
        };
    }

    /// <summary>Nothing, full, nothing: a glint winks on and off rather than drifting.</summary>
    private static CurveTexture Wink()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 0));
        curve.AddPoint(new Vector2(0.35f, 1));
        curve.AddPoint(new Vector2(1, 0));

        return new CurveTexture { Curve = curve };
    }

    private static Gradient Between(Color a, Color b)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, a);
        gradient.SetColor(1, b);

        return gradient;
    }

    private static Texture2D? Load(string file)
    {
        var path = "res://assets/particles/" + file;

        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }
}
