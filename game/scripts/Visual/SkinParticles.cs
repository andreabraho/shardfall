using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// The effects a cosmetic gives off (REF-23): frost, venom, embers or arcane light, as
/// particles from the Kenney Particle Pack (CC0).
/// </summary>
/// <remarks>
/// Two kinds of thing: a sword skin's effect coming off the length of its blade, and an aura's
/// stream of points wrapped round the blade. Drawn additively and unshaded, so they read as
/// light in any map's lighting.
/// </remarks>
public static class SkinParticles
{
    private const string Folder = "res://assets/particles/";

    /// <summary>
    /// A sword skin's particles of <paramref name="kind"/> in <paramref name="colour"/>, coming
    /// off a box of <paramref name="extents"/> (the blade), in world space, so a swung blade
    /// leaves a trail.
    /// </summary>
    public static GpuParticles3D? Build(string kind, Color colour, Vector3 extents)
    {
        var recipe = kind switch
        {
            // Motes of rime drifting down off the blade.
            "frost" => new Recipe("star_04.png", 0.14f, 1.4f, 14, new Vector3(0, -1, 0), 0.25f, 0.5f, -0.4f, 0f),
            // Green drops falling from the edge.
            "venom" => new Recipe("circle_05.png", 0.085f, 0.9f, 12, Vector3.Down, 0.1f, 0.3f, -3.2f, 0f),
            // Sparks rising and flickering out.
            "embers" => new Recipe("flare_01.png", 0.10f, 1.1f, 18, Vector3.Up, 0.6f, 1.3f, 0.8f, 0f),
            // Swirls turning as they climb.
            "arcane" => new Recipe("twirl_01.png", 0.25f, 1.6f, 10, Vector3.Up, 0.35f, 0.7f, 0.3f, 180f),
            _ => null,
        };

        if (recipe is null || Texture(recipe.Texture) is not { } texture) return null;

        var process = new ParticleProcessMaterial
        {
            Direction = recipe.Direction,
            Spread = 25f,
            InitialVelocityMin = recipe.SpeedMin,
            InitialVelocityMax = recipe.SpeedMax,
            Gravity = new Vector3(0, recipe.Gravity, 0),
            ScaleMin = 0.7f,
            ScaleMax = 1.3f,
            ColorRamp = Fade(colour),
            AngularVelocityMin = -recipe.Spin,
            AngularVelocityMax = recipe.Spin,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = extents,
        };

        return Particles("SkinParticles", recipe.Amount, recipe.Lifetime, process, Quad(texture, recipe.Size), local: false);
    }

    /// <summary>
    /// An aura on a blade: a dense stream of small points born round the blade's box of
    /// <paramref name="extents"/>, drifting a hand's breadth off it and going out — never still,
    /// never leaving the sword — and a few brighter glints among them.
    /// </summary>
    /// <param name="kind">The aura's effect; it picks the glints' shape. The points are the same for every aura.</param>
    /// <param name="shrink">What undoes the model's scale on a point's size.</param>
    public static IEnumerable<GpuParticles3D> SwordAura(string kind, Color colour, Vector3 extents, float shrink)
    {
        if (Texture("circle_05.png") is not { } point) yield break;

        // No haze of the colour any more (2026-09-28, at your call): it washed over the steel and
        // read as the sword changing colour. The aura is round the blade, not on it.

        // The points: many, small, short-lived and slow, so the eye reads a steady shimmer
        // that clings to the blade.
        yield return Particles("AuraPoints", 260, 0.5, Drift(colour, extents, 0.02f, 0.09f), Quad(point, 0.07f * shrink, shrinking: true), local: true);

        var glint = kind switch
        {
            "frost" => "star_04.png",
            "embers" => "flare_01.png",
            _ => "star_04.png",
        };

        if (Texture(glint) is not { } spark) yield break;

        // The glints: fewer and bigger, whiter at the heart, so the stream has a sparkle in it.
        var bright = colour.Lerp(Colors.White, 0.35f);

        yield return Particles("AuraGlints", 14, 0.7, Drift(bright, extents, 0.02f, 0.08f, spin: 90f), Quad(spark, 0.11f * shrink, shrinking: true), local: true);
    }

    /// <summary>
    /// Points born in a sheath round a blade of <paramref name="extents"/> — a hollow cylinder
    /// along its length, clear of the steel (2026-09-28) — drifting off a little every way.
    /// Born inside the blade's box, as they were, they sat on the sword and coloured it.
    /// </summary>
    private static ParticleProcessMaterial Drift(Color colour, Vector3 extents, float speedMin, float speedMax, float spin = 0f)
    {
        // The blade's length is its longest side; the sheath's width is the next.
        var sides = new[] { (extents.X, Vector3.Right), (extents.Y, Vector3.Up), (extents.Z, Vector3.Back) }
            .OrderByDescending(s => s.Item1)
            .ToArray();

        var (length, axis) = sides[0];
        var width = sides[1].Item1;

        var drift = DriftBody(colour, speedMin, speedMax, spin);

        drift.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
        drift.EmissionRingAxis = axis;
        drift.EmissionRingHeight = length * 2f;
        drift.EmissionRingInnerRadius = width * 1.15f;
        drift.EmissionRingRadius = width * 1.6f;

        return drift;
    }

    private static ParticleProcessMaterial DriftBody(Color colour, float speedMin, float speedMax, float spin) => new()
    {
        Direction = Vector3.Up,
        Spread = 180f,
        InitialVelocityMin = speedMin,
        InitialVelocityMax = speedMax,
        Gravity = new Vector3(0, 0.05f, 0),
        DampingMin = 0.2f,
        DampingMax = 0.4f,
        ScaleMin = 0.6f,
        ScaleMax = 1.3f,
        ScaleCurve = Dwindle(),
        ColorRamp = Fade(colour),
        AngularVelocityMin = -spin,
        AngularVelocityMax = spin,
    };

    private static Texture2D? Texture(string file)
    {
        if (ResourceLoader.Exists(Folder + file)) return ResourceLoader.Load<Texture2D>(Folder + file);

        GD.PushWarning($"[cosmetic] particle texture '{file}' is missing.");
        return null;
    }

    /// <summary>Full size at birth, nothing at the end: a point goes out rather than vanishing.</summary>
    private static CurveTexture Dwindle()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 1));
        curve.AddPoint(new Vector2(1, 0.15f));

        return new CurveTexture { Curve = curve };
    }

    private static GradientTexture1D Fade(Color colour)
    {
        var fade = new Gradient();
        fade.SetColor(0, colour with { A = 0 });
        fade.SetColor(1, colour with { A = 0 });
        fade.AddPoint(0.15f, colour);
        fade.AddPoint(0.7f, colour with { A = colour.A * 0.6f });

        return new GradientTexture1D { Gradient = fade };
    }

    private static QuadMesh Quad(Texture2D texture, float size, bool shrinking = false)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            // With the billboard, keep the size the particle process gives: its scale over life.
            BillboardKeepScale = shrinking,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = texture,
            NoDepthTest = false,
        };

        return new QuadMesh { Size = new Vector2(size, size), Material = material };
    }

    private static GpuParticles3D Particles(string name, int amount, double lifetime, ParticleProcessMaterial process, QuadMesh quad, bool local) => new()
    {
        Name = name,
        Amount = amount,
        Lifetime = lifetime,
        ProcessMaterial = process,
        DrawPass1 = quad,
        LocalCoords = local,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        VisibilityAabb = new Aabb(new Vector3(-3, -2, -3), new Vector3(6, 5, 6)),
    };

    private sealed record Recipe(
        string Texture, float Size, double Lifetime, int Amount, Vector3 Direction,
        float SpeedMin, float SpeedMax, float Gravity, float Spin);
}
