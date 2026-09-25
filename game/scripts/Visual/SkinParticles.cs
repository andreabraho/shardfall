using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// The effects a cosmetic gives off (REF-23): frost, venom, embers or arcane light, as
/// particles from the Kenney Particle Pack (CC0).
/// </summary>
/// <remarks>
/// One recipe per effect, shared by the blade skins and the aura; only where they come from
/// differs — the length of a blade, or a ring round the feet. Drawn additively and unshaded, so
/// they read as light in any map's lighting, and in world space, so a swung blade leaves a trail.
/// </remarks>
public static class SkinParticles
{
    private const string Folder = "res://assets/particles/";

    /// <summary>
    /// Particles of <paramref name="kind"/> in <paramref name="colour"/>, coming off a box of
    /// <paramref name="extents"/> (a blade), or off a ring round the feet when <paramref name="aura"/>.
    /// </summary>
    public static GpuParticles3D? Build(string kind, Color colour, Vector3 extents, bool aura)
    {
        var recipe = kind switch
        {
            // Motes of rime drifting down off the blade; round the feet, rising slowly.
            "frost" => new Recipe("star_04.png", 0.10f, 0.18f, 1.4f, aura ? 28 : 14, new Vector3(0, aura ? 1 : -1, 0), 0.25f, 0.5f, aura ? 0 : -0.4f, 0f),
            // Green drops falling from the edge.
            "venom" => new Recipe("circle_05.png", 0.06f, 0.11f, 0.9f, aura ? 26 : 12, Vector3.Down, 0.1f, 0.3f, -3.2f, 0f),
            // Sparks rising and flickering out.
            "embers" => new Recipe("flare_01.png", 0.06f, 0.14f, 1.1f, aura ? 36 : 18, Vector3.Up, 0.6f, 1.3f, 0.8f, 0f),
            // Swirls turning as they climb.
            "arcane" => new Recipe("twirl_01.png", 0.18f, 0.32f, 1.6f, aura ? 22 : 10, Vector3.Up, 0.35f, 0.7f, 0.3f, 180f),
            _ => null,
        };

        if (recipe is null) return null;

        var texture = ResourceLoader.Exists(Folder + recipe.Texture) ? ResourceLoader.Load<Texture2D>(Folder + recipe.Texture) : null;

        if (texture is null)
        {
            GD.PushWarning($"[cosmetic] particle texture '{recipe.Texture}' is missing.");
            return null;
        }

        var fade = new Gradient();
        fade.SetColor(0, colour with { A = 0 });
        fade.SetColor(1, colour with { A = 0 });
        fade.AddPoint(0.15f, colour);
        fade.AddPoint(0.7f, colour with { A = colour.A * 0.6f });

        var process = new ParticleProcessMaterial
        {
            Direction = recipe.Direction,
            Spread = 25f,
            InitialVelocityMin = recipe.SpeedMin,
            InitialVelocityMax = recipe.SpeedMax,
            Gravity = new Vector3(0, recipe.Gravity, 0),
            ScaleMin = 0.7f,
            ScaleMax = 1.3f,
            ColorRamp = new GradientTexture1D { Gradient = fade },
            AngularVelocityMin = -recipe.Spin,
            AngularVelocityMax = recipe.Spin,
        };

        if (aura)
        {
            process.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            process.EmissionRingAxis = Vector3.Up;
            process.EmissionRingRadius = 0.62f;
            process.EmissionRingInnerRadius = 0.45f;
            process.EmissionRingHeight = 0.1f;
        }
        else
        {
            process.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            process.EmissionBoxExtents = extents;
        }

        var quad = new QuadMesh
        {
            Size = new Vector2(recipe.Size, recipe.Size),
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                VertexColorUseAsAlbedo = true,
                AlbedoTexture = texture,
                NoDepthTest = false,
            },
        };

        return new GpuParticles3D
        {
            Name = "SkinParticles",
            Amount = recipe.Amount,
            Lifetime = recipe.Lifetime,
            ProcessMaterial = process,
            DrawPass1 = quad,
            LocalCoords = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-3, -2, -3), new Vector3(6, 5, 6)),
        };
    }

    private sealed record Recipe(
        string Texture, float SizeMin, float SizeMax, double Lifetime, int Amount, Vector3 Direction,
        float SpeedMin, float SpeedMax, float Gravity, float Spin)
    {
        public float Size => (SizeMin + SizeMax) / 2f;
    }
}
