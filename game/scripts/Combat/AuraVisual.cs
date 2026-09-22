using System.Collections.Generic;
using Godot;
using Kiln.Core.Combat;

namespace Kiln.Game.Combat;

/// <summary>
/// The aura worn while a self-buff is up: the Blade Aura, and Iron Skin (REF-03).
/// </summary>
/// <remarks>
/// It lights the character's own gear rather than wrapping them in a shape. A column of light
/// rising past the head hides the fight and reads as a pickup marker, not as a Warrior with a
/// burning sword — so what glows here is the sword itself for the Blade Aura, and the armour
/// for Iron Skin, with a low ring and a few embers at the feet to say where it comes from. The
/// colour says which buff it is, gold for the blade and steel-blue for the skin, and both can
/// be worn at once, which is why it is one node per status rather than one that switches.
/// <para>
/// It watches the status set instead of being switched on by the cast. A buff that expired,
/// was cleansed or was overwritten then takes its aura with it without anything having to
/// remember to turn it off.
/// </para>
/// </remarks>
public partial class AuraVisual : Node3D
{
    private static readonly Color Blade = new(1.0f, 0.78f, 0.32f);
    private static readonly Color Skin = new(0.60f, 0.78f, 1.0f);

    /// <summary>Mesh names that are a weapon on the character models in use.</summary>
    private static readonly string[] WeaponNames =
        ["sword", "blade", "axe", "hammer", "mace", "spear", "dagger", "weapon"];

    private MeshInstance3D? _inner;
    private MeshInstance3D? _outer;
    private GpuParticles3D? _motes;
    private StandardMaterial3D? _ringMaterial;
    private StandardMaterial3D? _glowMaterial;

    private readonly List<GeometryInstance3D> _lit = [];

    private Combatant? _self;
    private Node3D? _model;
    private double _spin;
    private float _strength;

    /// <summary>Which status this aura is the picture of.</summary>
    [Export] public StatusKind Kind { get; set; } = StatusKind.Empower;

    [Export] public float Radius { get; set; } = 0.85f;

    private Color Tint => Kind == StatusKind.Fortify ? Skin : Blade;

    public override void _Ready()
    {
        _self = GetParent().GetNodeOrNull<Combatant>("Combatant");

        _ringMaterial = Glow(Tint, 0.5f);

        // The light laid over the gear. One material shared by every mesh it lights, so a
        // pulse is one assignment rather than one per piece.
        _glowMaterial = new StandardMaterial3D
        {
            AlbedoColor = Tint with { A = 0f },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
            NoDepthTest = false,
        };

        // Flat rings on the ground: the character stays readable, and nothing rises past them.
        _inner = Ring(Radius * 0.60f, 0.045f, 0.03f);
        _outer = Ring(Radius, 0.04f, 0.01f);
        _motes = Motes(Tint);

        AddChild(_inner);
        AddChild(_outer);
        AddChild(_motes);

        Visible = false;
    }

    private static StandardMaterial3D Glow(Color colour, float alpha) => new()
    {
        AlbedoColor = colour with { A = alpha },
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private MeshInstance3D Ring(float radius, float thickness, float height) => new()
    {
        Name = $"Ring{radius:0.00}",
        Mesh = new TorusMesh
        {
            InnerRadius = radius - thickness,
            OuterRadius = radius + thickness,
            Rings = 24,
            RingSegments = 8,
        },
        MaterialOverride = _ringMaterial,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Position = new Vector3(0, 0.04f + height, 0),
    };

    /// <summary>
    /// The embers that drift up off the ring.
    /// </summary>
    /// <remarks>
    /// Kept knee-high on purpose: they say where the light is coming from without becoming a
    /// second column. Rings alone are geometry — they turn at a constant rate and the eye
    /// stops reading them after a second — and something leaving the ground at a slightly
    /// different moment each cycle is what makes the effect look alive.
    /// </remarks>
    private GpuParticles3D Motes(Color colour)
    {
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingRadius = Radius,
            EmissionRingInnerRadius = Radius * 0.55f,
            EmissionRingHeight = 0.05f,
            EmissionRingAxis = Vector3.Up,
            Direction = Vector3.Up,
            Spread = 6f,
            Gravity = new Vector3(0, 0.1f, 0),
            InitialVelocityMin = 0.25f,
            InitialVelocityMax = 0.5f,

            // A slow turn around the character, so the motes spiral with the rings rather
            // than rising in straight lines beside them.
            OrbitVelocityMin = 0.12f,
            OrbitVelocityMax = 0.28f,

            ScaleMin = 0.5f,
            ScaleMax = 1.0f,
            Color = colour,
        };

        // Fades in off the ground and out again before it reaches the waist: a mote that
        // vanished at full brightness would pop, and one that kept climbing would be a column.
        var fade = new Gradient();
        fade.SetColor(0, colour with { A = 0f });
        fade.SetColor(1, colour with { A = 0f });
        fade.AddPoint(0.3f, colour with { A = 0.85f });

        process.ColorRamp = new GradientTexture1D { Gradient = fade };

        var scale = new Curve();
        scale.AddPoint(new Vector2(0, 0.4f));
        scale.AddPoint(new Vector2(0.3f, 1f));
        scale.AddPoint(new Vector2(1, 0.1f));

        process.ScaleCurve = new CurveTexture { Curve = scale };

        return new GpuParticles3D
        {
            Name = "Motes",
            Amount = 22,
            Lifetime = 1.3,
            Randomness = 0.6f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh
            {
                Size = new Vector2(0.1f, 0.1f),
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                    VertexColorUseAsAlbedo = true,
                    AlbedoColor = colour,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.05f, 0),
            Emitting = false,
        };
    }

    // ------------------------------------------------------------------ the gear it lights

    /// <summary>
    /// Finds what this buff lights: the weapon for the Blade Aura, everything else for Iron
    /// Skin.
    /// </summary>
    /// <remarks>
    /// Looked up lazily and re-checked when the model changes, because the visual is rebuilt
    /// whenever a definition is applied and a mesh list taken at _Ready would be a list of
    /// freed nodes. A model with no recognisable weapon lights whatever it has rather than
    /// nothing at all — an aura the player cannot see is worse than one on the wrong mesh.
    /// </remarks>
    private void FindGear()
    {
        var root = GetParent().GetNodeOrNull<Visual.VisualRoot>("VisualRoot");
        var model = root?.GetChildCount() > 0 ? root.GetChild<Node>(0) as Node3D : null;

        if (model is null || (ReferenceEquals(model, _model) && _lit.Count > 0)) return;

        _model = model;
        _lit.Clear();

        var meshes = new List<MeshInstance3D>();

        Collect(model, meshes);

        foreach (var mesh in meshes)
        {
            if (IsWeapon(mesh.Name) == (Kind == StatusKind.Empower)) _lit.Add(mesh);
        }

        // Nothing matched: light the lot rather than nothing.
        if (_lit.Count == 0) _lit.AddRange(meshes);
    }

    private static void Collect(Node node, List<MeshInstance3D> into)
    {
        if (node is MeshInstance3D mesh && mesh.Visible) into.Add(mesh);

        foreach (var child in node.GetChildren()) Collect(child, into);
    }

    private static bool IsWeapon(string name)
    {
        var lower = name.ToLowerInvariant();

        foreach (var word in WeaponNames)
        {
            if (lower.Contains(word)) return true;
        }

        return false;
    }

    private void Light(float alpha)
    {
        if (_glowMaterial is null) return;

        _glowMaterial.AlbedoColor = Tint with { A = alpha };

        for (var i = _lit.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_lit[i]))
            {
                _lit.RemoveAt(i);
                continue;
            }

            // Assigned every frame it is lit and cleared once, rather than tracked: a model
            // swapped mid-buff would otherwise keep an overlay nothing owns any more.
            _lit[i].MaterialOverlay = alpha > 0 ? _glowMaterial : null;
        }
    }

    public override void _Process(double delta)
    {
        if (_self is null || _inner is null || _outer is null) return;

        // Fades in and out rather than snapping, so the end of a buff is something the eye
        // catches instead of a frame where it was simply gone.
        var wanted = _self.IsAlive && _self.Statuses.Has(Kind) ? 1f : 0f;

        _strength = Mathf.MoveToward(_strength, wanted, (float)delta * 3.2f);

        // Emission stops the moment the buff does, so the last motes finish their climb
        // instead of being cut off with the rings.
        if (_motes is not null) _motes.Emitting = wanted > 0;

        if (_strength <= 0.01f)
        {
            if (Visible) Light(0);

            Visible = false;
            return;
        }

        Visible = true;
        _spin += delta;

        FindGear();

        // Counter-rotating, at different speeds: two rings turning together read as one ring.
        _inner.Rotation = new Vector3(0, (float)_spin * 1.6f, 0);
        _outer.Rotation = new Vector3(0, (float)-_spin * 0.9f, 0);

        var pulse = 1f + (Mathf.Sin((float)_spin * 3.0f) * 0.05f);

        _inner.Scale = Vector3.One * pulse * _strength;
        _outer.Scale = Vector3.One * (2f - pulse) * _strength;

        if (_ringMaterial is not null)
        {
            _ringMaterial.AlbedoColor = Tint with { A = 0.5f * _strength };
        }

        // The gear breathes a little harder than the rings: it is the part being looked at.
        var breath = 0.5f + (Mathf.Sin((float)_spin * 3.4f) * 0.16f);

        Light(breath * _strength * (Kind == StatusKind.Fortify ? 0.5f : 1f));
    }
}
