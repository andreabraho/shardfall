using System.Collections.Generic;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Progression;
using Kiln.Game.Visual;

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
/// Mastery is written into it. The rank shifts the colour — amber at Master, violet at Grand
/// Master, deep violet with the gear burning gold at Perfect — and adds embers, width and brightness with it, so a skill
/// six hundred casts deep does not look like the one bought this morning.
/// </para>
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
    private static readonly Color Rage = new(1.0f, 0.36f, 0.22f);

    /// <summary>The blades wrapped in the Blade Aura's stream of points (REF-21), and the rank they were wrapped at.</summary>
    private readonly List<MeshInstance3D> _streamed = [];
    private MasteryRank? _streamRank;

    /// <summary>Mesh names that are a weapon on the character models in use.</summary>
    private static readonly string[] WeaponNames =
        ["sword", "blade", "axe", "hammer", "mace", "spear", "dagger", "weapon"];

    private MeshInstance3D? _inner;
    private MeshInstance3D? _outer;
    private GpuParticles3D? _motes;
    private StandardMaterial3D? _ringMaterial;
    private StandardMaterial3D? _glowMaterial;

    private readonly List<GeometryInstance3D> _lit = [];

    /// <summary>Embers an unranked aura throws off. Mastery adds to this.</summary>
    private const int BaseEmbers = 22;

    private Combatant? _self;
    private Node3D? _model;
    private MasteryRank? _shown;
    private double _spin;
    private float _strength;

    /// <summary>Which status this aura is the picture of.</summary>
    [Export] public StatusKind Kind { get; set; } = StatusKind.Empower;

    [Export] public float Radius { get; set; } = 0.85f;

    /// <summary>
    /// The rank of the skill that put this buff up, set by the caster at the moment of the
    /// cast. It decides the colour and how loud the whole thing is (REF-03).
    /// </summary>
    public MasteryRank Rank { get; set; } = MasteryRank.Normal;

    // The Asura's three (CLS-05, 2026-10-02, at your call): a glow of small points round the sword for
    // the Hexed Blade, an aura round the body for the Arcane Mail and for the Dread — and no rings.
    // Each has a colour of its own at every rank — Normal, Master, Grand Master, Perfect — and no two
    // of the three ever share one (at your call): rose, ember, magenta, gold for the blade; azure,
    // cyan, indigo, ice for the mail; violet, green, teal, bright violet for the dread.
    private static readonly Color[] Hex = [new(1.0f, 0.30f, 0.55f), new(1.0f, 0.48f, 0.18f), new(1.0f, 0.20f, 0.88f), new(1.0f, 0.86f, 0.36f)];
    private static readonly Color[] Mail = [new(0.34f, 0.64f, 1.0f), new(0.28f, 0.92f, 0.96f), new(0.46f, 0.44f, 1.0f), new(0.86f, 0.96f, 1.0f)];
    private static readonly Color[] Fear = [new(0.62f, 0.20f, 0.92f), new(0.46f, 0.88f, 0.34f), new(0.14f, 0.78f, 0.58f), new(0.74f, 0.34f, 1.0f)];

    /// <summary>The Asura's own colour for this aura at its rank, or null for an aura that takes the usual shift.</summary>
    private Color? Own => Kind switch
    {
        StatusKind.Leech => Hex[RankIndex],
        StatusKind.Thorns => Mail[RankIndex],
        StatusKind.Dread => Fear[RankIndex],
        _ => null,
    };

    private int RankIndex => System.Math.Clamp((int)Rank, 0, 3);

    private Color Base => Kind switch
    {
        StatusKind.Fortify => Blessing ? Jade : Skin,
        StatusKind.Frenzy => Rage,
        StatusKind.Leech => Hex[0],
        StatusKind.Thorns => Mail[0],
        StatusKind.Dread => Fear[0],
        StatusKind.Focus => Eye,
        _ => Blade,
    };

    /// <summary>An aura of points round the whole body, shoulder-high, rather than embers off a ring at the feet.</summary>
    private bool Shrouds => Kind is StatusKind.Thorns or StatusKind.Dread || Blessing;

    /// <summary>The Dragonseer's blessing (CLS-06, at your call): the same status as Iron Skin, worn as an aura round the body.</summary>
    private bool Blessing => Kind == StatusKind.Fortify && PlayerProfile.Class == Kiln.Core.Foundation.CharacterClass.Shaman;

    private static readonly Color Jade = new(0.50f, 1.0f, 0.78f);

    /// <summary>
    /// The Asura wears its own three: the Warrior's — which its Hexed Blade and its Arcane Mail also
    /// raise, for what they do — are not drawn on it.
    /// </summary>
    private bool Hidden => PlayerProfile.Class == Kiln.Core.Foundation.CharacterClass.Sura && Kind is StatusKind.Empower or StatusKind.Fortify;

    /// <summary>The points of light round each blade while the Hexed Blade is on.</summary>
    private readonly List<GpuParticles3D> _sparkles = [];
    private MasteryRank? _sparkleRank;

    /// <summary>
    /// A frenzy wears flames round the body and no rings (REF-21): three buffs up at once would
    /// otherwise stand the Warrior in six rings.
    /// </summary>
    private bool Rings => Kind is StatusKind.Empower or StatusKind.Fortify && !Shrouds;

    private Color Tint => Own ?? MasteryStyle.Tint(Base, Rank);

    /// <summary>What the gear burns: the same colour, except at Perfect, where it is gold.</summary>
    private Color GearTint => Own ?? MasteryStyle.Accent(Base, Rank);

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

        // A frenzy's flames lick up the body instead of drifting off the ring (REF-21).
        if (Kind == StatusKind.Frenzy && _motes.ProcessMaterial is ParticleProcessMaterial flames)
        {
            flames.EmissionRingRadius = 0.42f;
            flames.EmissionRingInnerRadius = 0.2f;
            flames.InitialVelocityMin = 0.9f;
            flames.InitialVelocityMax = 1.5f;
            _motes.Lifetime = 0.8;
        }

        // An aura round the body: points born all the way up it, turning slowly as they rise.
        if (Shrouds && _motes.ProcessMaterial is ParticleProcessMaterial shroud)
        {
            var fear = Kind == StatusKind.Dread;

            shroud.EmissionRingRadius = fear ? 0.85f : 0.55f;
            shroud.EmissionRingInnerRadius = fear ? 0.6f : 0.42f;
            shroud.EmissionRingHeight = fear ? 0.5f : 1.3f;
            shroud.InitialVelocityMin = fear ? 0.15f : 0.3f;
            shroud.InitialVelocityMax = fear ? 0.4f : 0.6f;
            shroud.OrbitVelocityMin = fear ? 0.5f : 0.2f;
            shroud.OrbitVelocityMax = fear ? 0.8f : 0.4f;
            _motes.Position = new Vector3(0, fear ? 0.45f : 0.8f, 0);
            _motes.Lifetime = 1.5;
        }

        Visible = false;
    }

    private static readonly Color Eye = new(1.0f, 0.82f, 0.36f);

    private readonly List<Node3D> _orbs = [];

    /// <summary>
    /// The Dragon's Eye (CLS-06, at your call): a small ball of light where the hand meets the fan,
    /// there for as long as the blessing lasts and going wherever the hand goes.
    /// </summary>
    private void Orb(bool on)
    {
        if (Kind != StatusKind.Focus) return;

        if (on && _orbs.Count > 0 && _orbs.TrueForAll(IsInstanceValid)) return;

        foreach (var old in _orbs)
        {
            if (!IsInstanceValid(old)) continue;

            old.GetParent()?.RemoveChild(old);
            old.QueueFree();
        }

        _orbs.Clear();

        if (!on) return;

        foreach (var lit in _lit)
        {
            if (lit is not MeshInstance3D mesh || !IsInstanceValid(mesh) || !IsWeapon(mesh.Name)) continue;

            var orb = new Node3D { Name = "EyeOrb" };

            foreach (var (texture, size, strength) in new[] { ("light_02.png", 0.62f, 0.75f), ("star_06.png", 0.36f, 1.0f) })
            {
                var path = "res://assets/particles/" + texture;

                orb.AddChild(new MeshInstance3D
                {
                    Mesh = new QuadMesh { Size = new Vector2(size, size) },
                    MaterialOverride = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                        AlbedoColor = Tint with { A = strength },
                        AlbedoTexture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null,
                        NoDepthTest = true,
                    },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }

            // The weapon's origin is its grip: the hand.
            mesh.AddChild(orb);
            _orbs.Add(orb);
        }
    }

    /// <summary>
    /// The Hexed Blade (CLS-05): small points of light drifting round each blade in hand, following
    /// it — not the Blade Aura's glowing shell.
    /// </summary>
    private void Sparkle(bool on)
    {
        if (Kind != StatusKind.Leech) return;

        if (on && _sparkleRank == Rank && _sparkles.Count > 0 && _sparkles.TrueForAll(IsInstanceValid)) return;

        foreach (var old in _sparkles)
        {
            if (!IsInstanceValid(old)) continue;

            old.GetParent()?.RemoveChild(old);
            old.QueueFree();
        }

        _sparkles.Clear();
        _sparkleRank = null;

        if (!on) return;

        _sparkleRank = Rank;

        foreach (var lit in _lit)
        {
            if (lit is not MeshInstance3D mesh || !IsInstanceValid(mesh) || !IsWeapon(mesh.Name) || mesh.Mesh is null) continue;

            var box = mesh.Mesh.GetAabb();
            var colour = Tint;
            var fade = new Gradient();

            fade.SetColor(0, colour with { A = 0f });
            fade.SetColor(1, colour with { A = 0f });
            fade.AddPoint(0.3f, colour with { A = 0.95f });

            var sparks = new GpuParticles3D
            {
                Name = "HexSparkle",
                Amount = 110 + (60 * RankIndex),
                Lifetime = 0.9,
                Randomness = 0.7f,
                LocalCoords = true,
                Position = box.GetCenter(),
                ProcessMaterial = new ParticleProcessMaterial
                {
                    EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                    EmissionBoxExtents = (box.Size * 0.5f) + new Vector3(0.08f, 0.08f, 0.08f),
                    Direction = Vector3.Up,
                    Spread = 180f,
                    Gravity = Vector3.Zero,
                    InitialVelocityMin = 0.05f,
                    InitialVelocityMax = 0.25f,
                    ScaleMin = 0.5f,
                    ScaleMax = 1.0f,
                    Color = colour,
                    ColorRamp = new GradientTexture1D { Gradient = fade },
                },
                DrawPass1 = new QuadMesh
                {
                    Size = new Vector2(0.075f, 0.075f),
                    Material = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                        VertexColorUseAsAlbedo = true,
                        AlbedoColor = colour,
                        AlbedoTexture = MoteTexture(),
                        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    },
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            mesh.AddChild(sparks);
            _sparkles.Add(sparks);
        }
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
            Amount = BaseEmbers,
            Lifetime = 1.3,
            Randomness = 0.6f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh
            {
                // Soft points of light since REF-21 (they were bare squares), and for a frenzy
                // tongues of flame licking up the body.
                Size = Kind == StatusKind.Frenzy ? new Vector2(0.5f, 0.5f) : Kind == StatusKind.Dread ? new Vector2(0.2f, 0.2f) : new Vector2(0.13f, 0.13f),
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                    VertexColorUseAsAlbedo = true,
                    AlbedoColor = colour,
                    AlbedoTexture = MoteTexture(),
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.05f, 0),
            Emitting = false,
        };
    }

    private Texture2D? MoteTexture()
    {
        var path = Kind == StatusKind.Frenzy ? "res://assets/particles/muzzle_02.png" : "res://assets/particles/circle_05.png";

        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    /// <summary>
    /// The Blade Aura's stream of points round the blade in hand (REF-21), in the colour the
    /// gear burns at this rank: on while the buff is up, rebuilt when the rank changes, gone
    /// when it ends. Beside a worn aura look, never in place of it.
    /// </summary>
    private void Stream(bool on)
    {
        if (Kind != StatusKind.Empower) return;

        if (on && _streamRank == Rank && _streamed.Count > 0 && _streamed.TrueForAll(IsInstanceValid)) return;

        foreach (var blade in _streamed)
        {
            if (IsInstanceValid(blade)) BladeAura.Remove(blade, "SkillAura");
        }

        _streamed.Clear();
        _streamRank = null;

        if (!on) return;

        foreach (var lit in _lit)
        {
            if (lit is not MeshInstance3D mesh || !IsInstanceValid(mesh) || !IsWeapon(mesh.Name) || mesh.Mesh is null) continue;

            if (BladeAura.Wrap(mesh, GearTint, "embers", "SkillAura", light: 0f) is not null) _streamed.Add(mesh);
        }

        _streamRank = Rank;
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

        // A weapon changed in hand leaves a freed mesh in the list: look again.
        if (model is null || (ReferenceEquals(model, _model) && _lit.Count > 0 && _lit.TrueForAll(IsInstanceValid))) return;

        _model = model;
        _lit.Clear();

        var meshes = new List<MeshInstance3D>();

        Collect(model, meshes);

        foreach (var mesh in meshes)
        {
            if (IsWeapon(mesh.Name) == (Kind is StatusKind.Empower or StatusKind.Leech or StatusKind.Focus)) _lit.Add(mesh);
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

        _glowMaterial.AlbedoColor = GearTint with { A = alpha };

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

    /// <summary>
    /// Re-colours everything for the rank now in force.
    /// </summary>
    /// <remarks>
    /// Called when the rank changes rather than every frame: the particle material is shared
    /// by every mote alive, and rewriting it each frame would restate the same colour sixty
    /// times a second for a value that changes twice a campaign.
    /// </remarks>
    private void Restyle()
    {
        if (_shown == Rank) return;

        _shown = Rank;

        var colour = Tint;

        if (_ringMaterial is not null) _ringMaterial.AlbedoColor = colour with { A = 0.5f };

        if (_motes?.ProcessMaterial is ParticleProcessMaterial process)
        {
            process.Color = colour;

            // Three stops, in the order the gradient sorts them: dark off the ground, bright
            // at a third of the way up, gone by the end.
            if (process.ColorRamp is GradientTexture1D ramp && ramp.Gradient is { } gradient && gradient.Offsets.Length == 3)
            {
                gradient.SetColor(0, colour with { A = 0f });
                gradient.SetColor(1, colour with { A = 0.85f });
                gradient.SetColor(2, colour with { A = 0f });
            }
        }

        if (_motes?.DrawPass1 is QuadMesh quad && quad.Material is StandardMaterial3D mote)
        {
            mote.AlbedoColor = colour;
        }

        // A mastered aura throws off more of everything: more embers, further out, and the
        // rings sit wider. This is the part the player reads across a field.
        if (_motes is not null)
        {
            _motes.Amount = Shrouds ? 170 + (80 * RankIndex) : BaseEmbers + MasteryStyle.Embers(Rank);
            _motes.Lifetime = (Kind == StatusKind.Frenzy ? 0.8 : Shrouds ? 1.5 : 1.3) + (MasteryStyle.Brightness(Rank) - 1f) * 0.5;
        }

        var spread = MasteryStyle.Flourish(Rank);

        if (_inner is not null) _inner.Scale = Vector3.One * spread;
        if (_outer is not null) _outer.Scale = Vector3.One * spread;
    }

    public override void _Process(double delta)
    {
        if (_self is null || _inner is null || _outer is null) return;

        Restyle();

        // Fades in and out rather than snapping, so the end of a buff is something the eye
        // catches instead of a frame where it was simply gone.
        var wanted = _self.IsAlive && _self.Statuses.Has(Kind) && !Hidden ? 1f : 0f;

        _strength = Mathf.MoveToward(_strength, wanted, (float)delta * 3.2f);

        // Emission stops the moment the buff does, so the last motes finish their climb
        // instead of being cut off with the rings.
        if (_motes is not null) _motes.Emitting = wanted > 0 && Kind is not (StatusKind.Leech or StatusKind.Focus);

        if (_strength <= 0.01f)
        {
            if (Visible) Light(0);

            Stream(false);
            Sparkle(false);
            Orb(false);
            Visible = false;
            return;
        }

        Visible = true;
        _spin += delta;

        FindGear();
        Stream(wanted > 0);
        Sparkle(wanted > 0);
        Orb(wanted > 0);

        // The ball breathes.
        foreach (var orb in _orbs)
        {
            if (IsInstanceValid(orb)) orb.Scale = Vector3.One * (1f + (Mathf.Sin((float)_spin * 4f) * 0.12f)) * _strength;
        }

        _inner.Visible = Rings;
        _outer.Visible = Rings;

        // Counter-rotating, at different speeds: two rings turning together read as one ring.
        _inner.Rotation = new Vector3(0, (float)_spin * 1.6f, 0);
        _outer.Rotation = new Vector3(0, (float)-_spin * 0.9f, 0);

        var pulse = 1f + (Mathf.Sin((float)_spin * 3.0f) * 0.05f);

        var spread = MasteryStyle.Flourish(Rank);

        _inner.Scale = Vector3.One * pulse * _strength * spread;
        _outer.Scale = Vector3.One * (2f - pulse) * _strength * spread;

        var glow = MasteryStyle.Brightness(Rank);

        if (_ringMaterial is not null)
        {
            _ringMaterial.AlbedoColor = Tint with { A = Mathf.Min(0.95f, (Kind == StatusKind.Fortify ? 0.3f : 0.5f) * glow) * _strength };
        }

        // The gear breathes a little harder than the rings: it is the part being looked at.
        // A Perfect skill breathes hardest, and never quite goes dim between beats.
        var breath = 0.5f + (Mathf.Sin((float)_spin * 3.4f) * 0.16f);

        // Iron Skin only tints the armour faintly: a body lit like the blade reads as the
        // character changing colour, when all it should say is "harder to hurt". The Blade
        // Aura lays nothing over the blade any more (2026-09-28, at your call: it turned the
        // sword yellow) — its stream of points round the blade is the whole effect.
        Light(Kind is StatusKind.Frenzy or StatusKind.Empower or StatusKind.Leech or StatusKind.Focus || Shrouds ? 0f : Mathf.Min(1f, breath * glow) * _strength * (Kind == StatusKind.Fortify ? 0.16f : 1f));
    }
}
