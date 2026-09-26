using System.Collections.Generic;
using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// What each skill looks like when it lands (REF-21): a cut sweeping across Cleave's arc, a
/// ring of wind round the Whirlwind, the ground breaking under a Ground Slam, a crescent flying
/// off the Blade Wave, sparks on everything a skill hits.
/// </summary>
/// <remarks>
/// Built from the Kenney Particle Pack (CC0) and nothing else: flat textured quads that grow,
/// turn, travel and fade, one-shot bursts of particles, and a scorch mark left on the ground.
/// Every piece frees itself when it is done, so a cast is fire-and-forget.
/// <para>
/// The flat flash on the ground that every area skill already draws (<see cref="AoeVisual"/>)
/// stays, quieter, because it is the honest statement of the reach; this is what the skill
/// looks like doing it.
/// </para>
/// </remarks>
public static class SkillFx
{
    private const string Folder = "res://assets/particles/";

    private static readonly Dictionary<string, Texture2D?> Textures = new(System.StringComparer.Ordinal);

    /// <summary>The dust thrown up by a blow on the ground: drawn over, not added, or it would glow.</summary>
    private static readonly Color Dust = new(0.72f, 0.66f, 0.56f, 0.55f);

    private static readonly Color Earth = new(0.42f, 0.34f, 0.26f, 1f);

    // ------------------------------------------------------------------ skills

    /// <summary>A crescent sweeping across the arc in front, and dust kicked up at its edge.</summary>
    public static void Cleave(Node context, Vector3 origin, Vector3 forward, float radius, float angle, Color colour, float bright = 1f)
    {
        var yaw = Yaw(forward);
        var sweep = Mathf.DegToRad(Mathf.Clamp(angle, 30f, 200f)) * 0.5f;

        // The cut, turning from one side of the arc to the other as it fades.
        var cut = Flat(context, "slash_02.png", origin + (Vector3.Up * 0.8f) + (forward * radius * 0.35f), radius * 1.5f, colour, 0.24, bright);
        cut.Rotation = new Vector3(0, yaw - (sweep * 0.5f), 0);
        cut.Spin = sweep / 0.24f;
        cut.From = 0.8f;
        cut.To = 1.1f;

        // A fainter echo a little wider, so the arc reads as swept rather than stamped.
        var echo = Flat(context, "slash_02.png", origin + (Vector3.Up * 0.7f) + (forward * radius * 0.4f), radius * 1.75f, colour with { A = colour.A * 0.45f }, 0.32, bright);
        echo.Rotation = new Vector3(0, yaw - (sweep * 0.7f), 0);
        echo.Spin = sweep * 1.2f / 0.32f;

        Burst(context, origin + (forward * radius * 0.7f), "smoke_04.png", Dust, 6, 0.9f, 0.4f, 1.1f, 0.6,
            Vector3.Up, 70f, -0.5f, emitRadius: radius * 0.3f, additive: false);
    }

    /// <summary>
    /// One of the Triple Cut's three cuts: a crescent across the front, tilted a different way
    /// each time — left, right, then straight down the middle — so the three read as three.
    /// </summary>
    public static void Cut(Node context, Vector3 origin, Vector3 forward, float radius, int hit, Color colour, float bright = 1f)
    {
        var yaw = Yaw(forward);
        var tilt = (hit % 3) switch { 0 => -0.45f, 1 => 0.45f, _ => 0f };
        var way = hit % 2 == 0 ? 1f : -1f;

        var cut = Flat(context, "slash_02.png", origin + (Vector3.Up * (0.7f + (0.12f * (hit % 3)))) + (forward * radius * 0.3f), radius * 1.3f, colour, 0.2, bright);
        cut.Rotation = new Vector3(0, yaw + tilt, 0);
        cut.Spin = 2.4f * way;
        cut.From = 0.75f;
        cut.To = 1.1f;
        cut.FadeIn = 0.04f;

        // The last of the three lands hardest.
        if (hit % 3 == 2)
        {
            Billboard(context, "star_06.png", origin + (Vector3.Up * 0.9f) + (forward * radius * 0.55f), 1.4f, colour, 0.16, bright);
        }
    }

    /// <summary>
    /// The Piercing Blow gathering itself: points of light drawn in from all round to a glow
    /// swelling at the blade's point, for as long as the charge lasts.
    /// </summary>
    public static void Gather(Node context, Vector3 origin, Vector3 forward, double seconds, Color colour, float bright = 1f)
    {
        var flat = (forward with { Y = 0 }).Normalized();
        var point = origin + (Vector3.Up * 0.95f) + (flat * 0.7f);

        Burst(context, point, "circle_05.png", colour, 36, 0.12f, 0f, 0f, seconds, Vector3.Up, 0f, 0f,
            emitRadius: 1.5f, radial: -3.2f, bright: bright);

        var glow = Billboard(context, "light_02.png", point, 1.4f, colour, seconds + 0.05, bright);
        glow.From = 0.15f;
        glow.To = 1.0f;
        glow.FadeIn = 0.5f;
        glow.Hold = 0.95f;
    }

    /// <summary>
    /// The Piercing Blow let go: a flash at the point, a shaft of light driven straight through
    /// the line, and a ring of force opening round it.
    /// </summary>
    public static void Pierce(Node context, Vector3 origin, Vector3 forward, float length, Color colour, float bright = 1f)
    {
        var yaw = Yaw(forward);
        var flat = (forward with { Y = 0 }).Normalized();

        Billboard(context, "star_06.png", origin + (Vector3.Up * 0.95f) + (flat * 0.8f), 2.2f, colour, 0.2, bright * 1.2f);

        // The shaft: long and narrow, lying level at chest height and racing out along the line.
        var shaft = Flat(context, "trace_06.png", origin + (Vector3.Up * 0.9f) + (flat * length * 0.5f), 1f, colour, 0.3, bright * 1.3f);
        shaft.Rotation = new Vector3(0, yaw, 0);
        shaft.Shape = new Vector2(1.8f, length * 1.1f);
        shaft.From = 0.3f;
        shaft.To = 1.0f;
        shaft.FadeIn = 0.02f;
        shaft.Hold = 0.25f;

        var ring = Flat(context, "circle_03.png", origin + (Vector3.Up * 0.9f) + (flat * length * 0.35f), 2.6f, colour, 0.3, bright);
        ring.Rotation = new Vector3(0, yaw, 0);
        ring.From = 0.2f;
        ring.To = 1.0f;
        ring.FadeIn = 0.02f;

        Burst(context, origin + (flat * length * 0.6f), "smoke_04.png", Dust, 6, 1.0f, 0.8f, 1.6f, 0.6, flat + (Vector3.Up * 0.3f), 30f, 0f,
            emitRadius: 0.5f, additive: false);
    }

    /// <summary>
    /// One turn of the Whirlwind: two rings of wind spinning round the Warrior, and dust driven
    /// outward along the ground.
    /// </summary>
    public static void Whirl(Node context, Vector3 center, float radius, Color colour, double seconds, int hit, float bright = 1f)
    {
        var way = hit % 2 == 0 ? 1f : -1f;
        var life = System.Math.Max(0.2, seconds * 1.3);

        var outer = Flat(context, "twirl_03.png", center + (Vector3.Up * 0.55f), radius * 2f, colour with { A = colour.A * 0.75f }, life, bright);
        outer.Spin = 13f * way;
        outer.From = 0.75f;
        outer.To = 1.0f;
        outer.Hold = 0.45f;

        var inner = Flat(context, "twirl_03.png", center + (Vector3.Up * 0.95f), radius * 1.3f, colour with { A = colour.A * 0.7f }, life, bright);
        inner.Rotation = new Vector3(0, Mathf.Pi, 0);
        inner.Spin = 17f * way;
        inner.From = 0.7f;
        inner.To = 1.0f;
        inner.Hold = 0.45f;

        // The blade's path, a crescent racing round at waist height.
        var edge = Flat(context, "slash_02.png", center + (Vector3.Up * 0.8f), radius * 1.6f, colour, life * 0.8, bright);
        edge.Spin = 20f * way;

        Burst(context, center, "smoke_04.png", Dust, 10, 1.0f, 0f, 0f, 0.7, Vector3.Up, 10f, 0f,
            emitRadius: radius * 0.35f, radial: 3.2f, additive: false, ring: true);
    }

    /// <summary>
    /// The Ground Slam: a shock ring racing out to the edge, a flash, earth thrown up, a cloud
    /// of dust rolling out, a scorch left on the ground, and the camera jolted.
    /// </summary>
    public static void Slam(Node context, Vector3 center, float radius, Color colour, float bright = 1f, bool quiet = false)
    {
        var ring = Flat(context, "circle_03.png", center + (Vector3.Up * 0.12f), radius * 2.2f, colour, 0.5, bright);
        ring.From = 0.12f;
        ring.To = 1.0f;
        ring.FadeIn = 0.02f;
        ring.Hold = 0.2f;

        var inner = Flat(context, "light_02.png", center + (Vector3.Up * 0.1f), radius * 1.6f, colour, 0.35, bright);
        inner.From = 0.1f;
        inner.To = 0.8f;
        inner.FadeIn = 0.02f;

        Billboard(context, "star_06.png", center + (Vector3.Up * 0.6f), 3.2f, colour, 0.18, bright);

        Burst(context, center, "dirt_02.png", Earth, 22, 0.42f, 4f, 7.5f, 0.9, Vector3.Up, 50f, -15f,
            emitRadius: 0.8f, additive: false, spin: 6f);

        Burst(context, center, "smoke_04.png", Dust, 18, 1.7f, 0f, 0f, 1.2, Vector3.Up, 10f, 0.2f,
            emitRadius: 0.8f, radial: 4.2f, additive: false, ring: true);

        Scorch(context, center, radius * 1.25f, quiet ? 0.1 : 2.6, quiet ? 0.01f : 0.85f);

        if (!quiet) Camera.CameraRig.Kick(Vector3.Zero, 0.5f);
    }

    /// <summary>
    /// The Blade Wave: a crescent of light flying straight ahead along its line, points shed in
    /// its wake, and a streak left on the ground behind it.
    /// </summary>
    public static void Wave(Node context, Vector3 origin, Vector3 forward, float length, float width, Color colour, float bright = 1f)
    {
        const double flight = 0.32;

        var yaw = Yaw(forward);

        var crescent = Flat(context, "slash_02.png", origin + (Vector3.Up * 0.9f) + (forward * 0.6f), width * 2.4f, colour, flight, bright * 1.3f);
        crescent.Rotation = new Vector3(0, yaw, 0);
        crescent.Velocity = forward * (length / (float)flight);
        crescent.From = 0.7f;
        crescent.To = 1.15f;
        crescent.FadeIn = 0.05f;
        crescent.Hold = 0.75f;

        // Points shed along the way, left where they fell as it flies on. On a carrier of their
        // own rather than on the crescent, whose scale would scale them too.
        if (Root(context) is { } root)
        {
            var carrier = new FxCarrier { Velocity = crescent.Velocity, Flight = flight, Linger = 0.45 };
            root.AddChild(carrier);
            carrier.GlobalPosition = crescent.GlobalPosition;
            carrier.Rotation = new Vector3(0, yaw, 0);
            carrier.AddChild(Emitter("circle_05.png", colour, 70, 0.12f, 0.4, new Vector3(width * 0.5f, 0.25f, 0.1f), bright));
        }

        var streak = Flat(context, "trace_06.png", origin + (Vector3.Up * 0.08f) + (forward * length * 0.5f), 1f, colour with { A = colour.A * 0.6f }, 0.55, bright);
        streak.Rotation = new Vector3(0, yaw, 0);
        streak.Shape = new Vector2(width * 0.9f, length);
        streak.FadeIn = 0.3f;
        streak.Hold = 0.4f;
    }

    /// <summary>A blow that lands on one target: Heavy Strike, Shield Bash.</summary>
    public static void Strike(Node context, Vector3 at, Color colour, bool bash, float bright = 1f)
    {
        Impact(context, at, colour, 1.5f, bright);

        var ring = Flat(context, "circle_03.png", at + (Vector3.Up * 0.1f), 2.4f, colour, 0.35, bright);
        ring.From = 0.2f;
        ring.To = 1.0f;
        ring.FadeIn = 0.02f;

        if (bash) Billboard(context, "muzzle_02.png", at + (Vector3.Up * 1.0f), 1.3f, colour, 0.2, bright);
        else Burst(context, at, "dirt_02.png", Earth, 8, 0.3f, 2.5f, 4.5f, 0.6, Vector3.Up, 45f, -12f, emitRadius: 0.3f, additive: false, spin: 6f);
    }

    /// <summary>Iron Skin cast: a ring closing in on the Warrior, a sigil turning at the feet, sparks rising.</summary>
    public static void Harden(Node context, Vector3 center, Color colour, float bright = 1f)
    {
        var ring = Flat(context, "circle_03.png", center + (Vector3.Up * 0.1f), 3.2f, colour, 0.45, bright);
        ring.From = 1.2f;
        ring.To = 0.35f;

        var sigil = Flat(context, "magic_03.png", center + (Vector3.Up * 0.06f), 2.2f, colour, 0.9, bright);
        sigil.Spin = 3f;
        sigil.FadeIn = 0.15f;
        sigil.Hold = 0.5f;

        Burst(context, center + (Vector3.Up * 0.8f), "circle_05.png", colour, 20, 0.09f, 1.5f, 3f, 0.6, Vector3.Up, 60f, -2f, emitRadius: 0.45f, bright: bright);
    }

    /// <summary>Battle Frenzy cast: a burst of flame round the Warrior and a shock ring.</summary>
    public static void Frenzy(Node context, Vector3 center, Color colour, float bright = 1f)
    {
        Burst(context, center + (Vector3.Up * 0.2f), "muzzle_02.png", colour, 28, 0.9f, 2.5f, 4.5f, 0.6, Vector3.Up, 15f, 1f,
            emitRadius: 0.55f, ring: true, bright: bright);

        var ring = Flat(context, "circle_03.png", center + (Vector3.Up * 0.1f), 3.4f, colour, 0.4, bright);
        ring.From = 0.2f;
        ring.To = 1.1f;
        ring.FadeIn = 0.02f;

        Billboard(context, "star_06.png", center + (Vector3.Up * 1.1f), 1.8f, colour, 0.2, bright);
    }

    /// <summary>The Guard shaking off harmful effects: a pale flash and points rising off the Warrior.</summary>
    public static void Cleanse(Node context, Vector3 center)
    {
        var colour = new Color(0.85f, 0.95f, 1f);

        Billboard(context, "star_06.png", center + (Vector3.Up * 1.2f), 1.8f, colour, 0.25);
        Burst(context, center + (Vector3.Up * 0.6f), "circle_05.png", colour, 24, 0.1f, 1.2f, 2.4f, 0.7, Vector3.Up, 25f, 1.5f, emitRadius: 0.5f);
    }

    /// <summary>Blade Aura cast: a flash on the raised blade and a ring at the feet.</summary>
    public static void Sharpen(Node context, Vector3 center, Color colour, float bright = 1f)
    {
        Billboard(context, "star_06.png", center + (Vector3.Up * 1.6f), 2.0f, colour, 0.3, bright);

        var ring = Flat(context, "light_02.png", center + (Vector3.Up * 0.08f), 2.6f, colour, 0.5, bright);
        ring.From = 0.3f;
        ring.To = 1.0f;
    }

    /// <summary>
    /// A hit landing: a flare and a spray of sparks where the target stands. Every creature a
    /// skill touches shows one, so a skill that hit five reads as having hit five.
    /// </summary>
    public static void Impact(Node context, Vector3 at, Color colour, float scale = 1f, float bright = 1f)
    {
        var chest = at + (Vector3.Up * 1.0f);

        var flare = Billboard(context, "star_06.png", chest, 1.1f * scale, colour.Lerp(Colors.White, 0.4f), 0.22, bright);
        flare.From = 0.5f;
        flare.To = 1.3f;
        flare.FadeIn = 0.02f;
        flare.Hold = 0.1f;

        Burst(context, chest, "circle_05.png", colour.Lerp(Colors.White, 0.3f), (int)(12 * scale), 0.08f, 3f, 6f, 0.35,
            Vector3.Up, 180f, -9f, bright: bright);
    }

    /// <summary>
    /// Plays every effect once, almost invisible, where the player stands: the first cast of a
    /// session no longer stalls while the renderer prepares what it has never drawn.
    /// </summary>
    public static void Prewarm(Node context, Vector3 at)
    {
        var faint = new Color(1, 1, 1, 0.01f);
        var ahead = Vector3.Forward;

        // Every variant: each different set of particle settings is a shader of its own.
        Cleave(context, at, ahead, 3f, 100f, faint);
        Whirl(context, at, 3f, faint, 0.3, 0);
        Wave(context, at, ahead, 4f, 2f, faint);
        Cut(context, at, ahead, 3f, 2, faint);
        Gather(context, at, ahead, 0.2, faint);
        Pierce(context, at, ahead, 4f, faint);
        Strike(context, at, faint, bash: true);
        Strike(context, at, faint, bash: false);
        Harden(context, at, faint);
        Frenzy(context, at, faint);
        Sharpen(context, at, faint);
        Impact(context, at, faint);
        Cleanse(context, at + (Vector3.Down * 50));

        // The slam without its jolt, and its scorch all but invisible.
        Slam(context, at, 1f, faint, quiet: true);
    }

    // ------------------------------------------------------------------ pieces

    private static float Yaw(Vector3 forward)
    {
        var flat = forward with { Y = 0 };

        return flat.LengthSquared() < 0.0001f ? 0f : Mathf.Atan2(flat.X, flat.Z);
    }

    private static Texture2D? Texture(string file)
    {
        if (Textures.TryGetValue(file, out var known)) return known;

        var path = Folder + file;
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;

        if (texture is null) GD.PushWarning($"[skill fx] texture '{file}' is missing.");

        Textures[file] = texture;
        return texture;
    }

    private static Node? Root(Node context) =>
        context.IsInsideTree() ? context.GetTree().CurrentScene ?? context.GetTree().Root : null;

    /// <summary>A quad lying on the ground (or level at some height), its image's bottom edge toward +Z.</summary>
    private static FxQuad Flat(Node context, string texture, Vector3 at, float size, Color colour, double life, float bright = 1f)
    {
        var quad = new FxQuad
        {
            Life = life,
            Colour = colour,
            Bright = bright,
            Shape = new Vector2(size, size),
            Mesh = new PlaneMesh { Size = Vector2.One },
            MaterialOverride = Material(texture, billboard: false, additive: true),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        Place(context, quad, at);
        return quad;
    }

    /// <summary>
    /// A quad always facing the camera. These are the flares: with fewer flashes asked for
    /// (UIX-04) they are drawn at a third of their strength.
    /// </summary>
    private static FxQuad Billboard(Node context, string texture, Vector3 at, float size, Color colour, double life, float bright = 1f)
    {
        if (Settings.GameSettings.ReduceFlashes) colour = colour with { A = colour.A * 0.35f };

        var quad = new FxQuad
        {
            Life = life,
            Colour = colour,
            Bright = bright,
            Shape = new Vector2(size, size),
            Mesh = new QuadMesh { Size = Vector2.One },
            MaterialOverride = Material(texture, billboard: true, additive: true),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        Place(context, quad, at);
        return quad;
    }

    private static void Place(Node context, Node3D node, Vector3 at)
    {
        if (Root(context) is not { } root)
        {
            node.QueueFree();
            return;
        }

        root.AddChild(node);
        node.GlobalPosition = at;
    }

    private static StandardMaterial3D Material(string texture, bool billboard, bool additive) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
        BillboardKeepScale = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoTexture = Texture(texture),
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };

    /// <summary>A one-shot spray of particles, freed when the last one has faded.</summary>
    private static void Burst(Node context, Vector3 at, string texture, Color colour, int amount, float size,
        float speedMin, float speedMax, double life, Vector3 direction, float spread, float gravity,
        float emitRadius = 0f, float radial = 0f, bool additive = true, bool ring = false, float spin = 0f, float bright = 1f)
    {
        if (Root(context) is not { } root) return;

        var process = new ParticleProcessMaterial
        {
            Direction = direction,
            Spread = spread,
            InitialVelocityMin = speedMin,
            InitialVelocityMax = speedMax,
            Gravity = new Vector3(0, gravity, 0),
            ScaleMin = 0.6f,
            ScaleMax = 1.3f,
            ScaleCurve = Dwindle(),
            ColorRamp = Fade(colour, additive ? (Settings.GameSettings.ReduceFlashes ? Mathf.Min(bright, 1f) : bright) : 1f),
            AngleMin = -180f,
            AngleMax = 180f,
            AngularVelocityMin = -spin * 57f,
            AngularVelocityMax = spin * 57f,
            DampingMin = radial > 0 ? 2.5f : 0f,
            DampingMax = radial > 0 ? 4f : 0f,
        };

        if (radial > 0)
        {
            process.RadialVelocityMin = radial * 0.7f;
            process.RadialVelocityMax = radial;
        }

        if (ring)
        {
            process.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
            process.EmissionRingAxis = Vector3.Up;
            process.EmissionRingRadius = Mathf.Max(0.05f, emitRadius);
            process.EmissionRingInnerRadius = Mathf.Max(0f, emitRadius * 0.6f);
            process.EmissionRingHeight = 0.05f;
        }
        else if (emitRadius > 0)
        {
            process.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            process.EmissionSphereRadius = emitRadius;
        }

        var particles = new GpuParticles3D
        {
            Amount = System.Math.Max(1, amount),
            Lifetime = life,
            OneShot = true,
            Explosiveness = 0.9f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh
            {
                Size = new Vector2(size, size),
                Material = ParticleMaterial(texture, additive),
            },
            LocalCoords = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-8, -2, -8), new Vector3(16, 10, 16)),
            Emitting = true,
        };

        root.AddChild(particles);
        particles.GlobalPosition = at;

        particles.GetTree().CreateTimer(life + 0.3).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(particles)) particles.QueueFree();
        };
    }

    /// <summary>A stream of points shed as its parent moves, left behind in the world.</summary>
    private static GpuParticles3D Emitter(string texture, Color colour, int amount, float size, double life, Vector3 box, float bright)
    {
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = box,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 0.1f,
            InitialVelocityMax = 0.5f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.6f,
            ScaleMax = 1.3f,
            ScaleCurve = Dwindle(),
            ColorRamp = Fade(colour, bright),
        };

        return new GpuParticles3D
        {
            Amount = amount,
            Lifetime = life,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(size, size), Material = ParticleMaterial(texture, additive: true) },
            LocalCoords = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-12, -2, -12), new Vector3(24, 6, 24)),
        };
    }

    private static StandardMaterial3D ParticleMaterial(string texture, bool additive) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        BillboardKeepScale = true,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = Texture(texture),
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };

    /// <summary>A scorch left where the ground was struck, fading over a few seconds.</summary>
    private static void Scorch(Node context, Vector3 at, float size, double life, float strength = 0.85f)
    {
        if (Root(context) is not { } root || Texture("scorch_03.png") is not { } texture) return;

        var decal = new Decal
        {
            TextureAlbedo = texture,
            Size = new Vector3(size, 2f, size),
            Modulate = new Color(0.10f, 0.08f, 0.06f, strength),
            AlbedoMix = 1f,
            CullMask = 1,
        };

        root.AddChild(decal);
        decal.GlobalPosition = at + (Vector3.Up * 0.5f);
        decal.Rotation = new Vector3(0, (float)GD.RandRange(0, Mathf.Tau), 0);

        var tween = decal.CreateTween();
        tween.TweenProperty(decal, "modulate:a", 0f, life * 0.5).SetDelay(life * 0.5);
        tween.TweenCallback(Callable.From(decal.QueueFree));
    }

    private static CurveTexture Dwindle()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 0.7f));
        curve.AddPoint(new Vector2(0.2f, 1f));
        curve.AddPoint(new Vector2(1, 0.3f));

        return new CurveTexture { Curve = curve };
    }

    private static GradientTexture1D Fade(Color colour, float bright)
    {
        var lit = colour with { R = colour.R * bright, G = colour.G * bright, B = colour.B * bright };
        var fade = new Gradient();
        fade.SetColor(0, lit);
        fade.SetColor(1, lit with { A = 0 });
        fade.AddPoint(0.6f, lit with { A = lit.A * 0.7f });

        return new GradientTexture1D { Gradient = fade };
    }
}

/// <summary>
/// What carries a trail of particles along a flying effect: it moves for its flight, stops
/// shedding, and lingers until what it shed has faded (REF-21).
/// </summary>
public partial class FxCarrier : Node3D
{
    public Vector3 Velocity { get; set; }
    public double Flight { get; set; } = 0.3;
    public double Linger { get; set; } = 0.4;

    private double _age;

    public override void _Process(double delta)
    {
        _age += delta;

        if (_age < Flight)
        {
            GlobalPosition += Velocity * (float)delta;
            return;
        }

        foreach (var child in GetChildren())
        {
            if (child is GpuParticles3D particles) particles.Emitting = false;
        }

        if (_age >= Flight + Linger) QueueFree();
    }
}

/// <summary>
/// One flat piece of a skill's effect: it grows from <see cref="From"/> to <see cref="To"/>,
/// turns, travels, fades in fast and out slower, and frees itself (REF-21).
/// </summary>
public partial class FxQuad : MeshInstance3D
{
    public double Life { get; set; } = 0.3;
    public Color Colour { get; set; } = Colors.White;
    public float Bright { get; set; } = 1f;

    /// <summary>Its width and depth (or height, facing the camera) at full size.</summary>
    public Vector2 Shape { get; set; } = Vector2.One;

    public float From { get; set; } = 1f;
    public float To { get; set; } = 1f;

    /// <summary>Radians a second about its own up.</summary>
    public float Spin { get; set; }

    public Vector3 Velocity { get; set; }

    /// <summary>The part of its life spent fading in.</summary>
    public float FadeIn { get; set; } = 0.08f;

    /// <summary>The part of its life it holds full before fading out.</summary>
    public float Hold { get; set; } = 0.3f;

    private double _age;

    public override void _Ready()
    {
        // Nothing burns brighter than its own colour with fewer flashes asked for (UIX-04).
        if (Settings.GameSettings.ReduceFlashes) Bright = Mathf.Min(Bright, 1f);

        Step(0);
    }

    public override void _Process(double delta)
    {
        _age += delta;

        if (_age >= Life)
        {
            QueueFree();
            return;
        }

        if (Spin != 0) RotateY(Spin * (float)delta);
        if (Velocity != Vector3.Zero) GlobalPosition += Velocity * (float)delta;

        Step((float)(_age / Life));
    }

    private void Step(float t)
    {
        // Eased out: most of the growth happens early, as a blow's does.
        var grow = Mathf.Lerp(From, To, 1f - ((1f - t) * (1f - t)));
        Scale = new Vector3(Shape.X * grow, 1f, Shape.Y * grow);

        if (Mesh is QuadMesh) Scale = new Vector3(Shape.X * grow, Shape.Y * grow, 1f);

        var alpha = t < FadeIn ? t / Mathf.Max(0.001f, FadeIn)
            : t < Hold ? 1f
            : 1f - ((t - Hold) / Mathf.Max(0.001f, 1f - Hold));

        if (MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = new Color(Colour.R * Bright, Colour.G * Bright, Colour.B * Bright, Colour.A * Mathf.Clamp(alpha, 0f, 1f));
        }
    }
}
