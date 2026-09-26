using System.Collections.Generic;
using Godot;
using Kiln.Core.Progression;

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
        public StandardMaterial3D? Material { get; init; }

        /// <summary>Set instead of <see cref="Material"/> for a swirling flash.</summary>
        public ShaderMaterial? Swirl { get; init; }

        public required Color Color { get; init; }

        /// <summary>How long this one lasts. A swirl stays up a little longer, to be seen turning.</summary>
        public double Lifetime { get; init; }

        /// <summary>Rank multiplier on how solid the flash is drawn.</summary>
        public float Brightness { get; init; } = 1f;
        public required float Radius { get; init; }
        public double Elapsed { get; set; }

        /// <summary>
        /// How solid it is at full strength. A player's own skill draws its reach faintly since
        /// REF-21, the skill's effect doing the showing; an enemy's warning stays solid.
        /// </summary>
        public float Solid { get; init; } = 0.45f;
    }

    private readonly List<Flash> _active = [];
    private readonly List<MeshInstance3D> _pool = [];
    private readonly List<MeshInstance3D> _swirlPool = [];

    /// <summary>
    /// The swirl: streaks twisting outward through the wedge and ripples racing to its rim.
    /// </summary>
    /// <remarks>
    /// For a spinning skill. A flat wedge of one colour says where a blow landed; it does not
    /// say the blow was a spin. The pattern is drawn from each point's angle and distance from
    /// the Warrior, turning with time, so the flash looks thrown out by the turn rather than
    /// laid on the floor — and it is still exactly the wedge the hit covered.
    /// </remarks>
    private static readonly Shader SwirlShader = new()
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, depth_test_disabled, blend_add;

            uniform vec4 tint : source_color = vec4(0.55, 0.85, 1.0, 1.0);
            uniform float fade = 1.0;
            uniform float radius = 6.0;
            uniform float half_angle = 1.4;

            varying vec2 local_xz;

            void vertex() {
                local_xz = VERTEX.xz;
            }

            void fragment() {
                float r = length(local_xz) / max(radius, 0.001);
                float a = atan(local_xz.x, local_xz.y);

                float twist = a * 5.0 + r * 9.0 - TIME * 14.0;
                float streak = pow(0.5 + 0.5 * sin(twist), 3.0);
                float ripple = 0.5 + 0.5 * sin(r * 22.0 - TIME * 18.0);

                float rim = smoothstep(0.72, 1.0, r);
                float side = smoothstep(half_angle - 0.25, half_angle, abs(a));

                float glow = clamp(0.18 + 0.62 * streak * (0.55 + 0.45 * ripple) + rim * 0.55 + side * 0.4, 0.0, 1.0);

                ALBEDO = tint.rgb * (0.6 + glow);
                ALPHA = glow * fade;
            }
            """,
    };

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

        _preview.Mesh = AoeGeometry.Fan(radius, 360f);
        _preview.GlobalPosition = center + (Vector3.Up * 0.06f);
        _preview.Visible = true;

        _previewPulse += GetProcessDeltaTime() * 4.0;
        _previewMaterial!.AlbedoColor = FriendlyColor with
        {
            A = 0.20f + (0.10f * (float)Mathf.Sin(_previewPulse)),
        };
    }

    /// <summary>A filled circle centred on a point.</summary>
    public static void Circle(Vector3 center, float radius, bool hostile = false,
        MasteryRank rank = MasteryRank.Normal)
        => _instance?.Spawn(center, radius, Vector3.Zero, 360f, hostile, rank);

    /// <summary>A wedge opening along <paramref name="forward"/>.</summary>
    public static void Cone(Vector3 origin, Vector3 forward, float radius, float angleDegrees,
        bool hostile = false, MasteryRank rank = MasteryRank.Normal, bool swirl = false)
        => _instance?.Spawn(origin, radius, forward, angleDegrees, hostile, rank, swirl);

    private void Spawn(Vector3 origin, float radius, Vector3 forward, float angleDegrees, bool hostile,
        MasteryRank rank, bool swirl = false)
    {
        var mesh = swirl ? RentSwirl() : Rent();

        mesh.Mesh = AoeGeometry.Fan(radius, angleDegrees);
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

        // A mastered skill burns in its rank's colour, and a little brighter with it: the
        // work that went into a skill should be visible in what it does, not only in its
        // tooltip (REF-03).
        // Warmed toward the rank's second colour, so a Perfect skill throws a violet flash
        // with gold in it rather than the flat violet of Grand Master.
        var own = hostile ? HostileColor : FriendlyColor;
        var color = Visual.MasteryStyle.Tint(own, rank).Lerp(Visual.MasteryStyle.Accent(own, rank), 0.3f);

        mesh.Visible = true;

        if (swirl && mesh.MaterialOverride is ShaderMaterial shader)
        {
            shader.SetShaderParameter("tint", color);
            shader.SetShaderParameter("radius", radius);
            shader.SetShaderParameter("half_angle", Mathf.DegToRad(angleDegrees) / 2f);
            shader.SetShaderParameter("fade", 1f);

            _active.Add(new Flash
            {
                Mesh = mesh,
                Swirl = shader,
                Color = color,
                Radius = radius,
                Brightness = Visual.MasteryStyle.Brightness(rank),
                Lifetime = Duration * 1.35,
            });

            return;
        }

        var material = (StandardMaterial3D)mesh.MaterialOverride;
        material.AlbedoColor = color;

        _active.Add(new Flash
        {
            Mesh = mesh,
            Material = material,
            Color = color,
            Radius = radius,
            Brightness = Visual.MasteryStyle.Brightness(rank),
            Lifetime = Duration,
            Solid = hostile ? 0.45f : 0.2f,
        });
    }

    private MeshInstance3D RentSwirl()
    {
        if (_swirlPool.Count > 0)
        {
            var reused = _swirlPool[^1];
            _swirlPool.RemoveAt(_swirlPool.Count - 1);
            return reused;
        }

        // One material per pooled mesh: its colour and fade are its own, and two swirls from
        // consecutive hits are on screen at once.
        var instance = new MeshInstance3D
        {
            MaterialOverride = new ShaderMaterial { Shader = SwirlShader, RenderPriority = 3 },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
        };

        AddChild(instance);
        return instance;
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

            if (flash.Elapsed >= flash.Lifetime)
            {
                flash.Mesh.Visible = false;
                (flash.Swirl is null ? _pool : _swirlPool).Add(flash.Mesh);
                _active.RemoveAt(i);
                continue;
            }

            var t = (float)(flash.Elapsed / flash.Lifetime);

            if (flash.Swirl is not null)
            {
                // Grows a little further than a flat flash, and fades on a curve that holds
                // its brightness longer: the streaks need a moment on screen to be seen moving.
                var grow = Mathf.Lerp(0.8f, 1.08f, t);
                flash.Mesh.Scale = new Vector3(grow, 1, grow);
                flash.Swirl.SetShaderParameter("fade",
                    Mathf.Min(1f, 0.75f * flash.Brightness) * (1f - (t * t)));

                continue;
            }

            // Snap to full size immediately, then fade: the player needs to see the extent
            // at the moment of the hit, not watch it grow afterwards.
            var scale = Mathf.Lerp(0.85f, 1.05f, t);
            flash.Mesh.Scale = new Vector3(scale, 1, scale);
            flash.Material!.AlbedoColor = flash.Color with
            {
                A = Mathf.Min(0.9f, flash.Solid * flash.Brightness) * (1f - t),
            };
        }
    }
}
