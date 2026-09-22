using Godot;
using Kiln.Core.Combat;

namespace Kiln.Game.Combat;

/// <summary>
/// The aura worn while a self-buff is up: the Blade Aura, and Iron Skin (REF-03).
/// </summary>
/// <remarks>
/// Built the way the original's buff auras read rather than as a status icon in the corner:
/// two rings turning against each other at the character's feet and a soft column of light
/// rising through them, so a buffed Warrior is recognisable from across the field and from
/// behind. The colour says which buff it is — gold for the blade, steel-blue for the skin —
/// and both can be worn at once, which is why it is one node per status rather than one node
/// that switches colour.
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

    private MeshInstance3D? _inner;
    private MeshInstance3D? _outer;
    private MeshInstance3D? _column;
    private StandardMaterial3D? _ringMaterial;
    private StandardMaterial3D? _columnMaterial;

    private Combatant? _self;
    private double _spin;
    private float _strength;

    /// <summary>Which status this aura is the picture of.</summary>
    [Export] public StatusKind Kind { get; set; } = StatusKind.Empower;

    [Export] public float Radius { get; set; } = 0.95f;

    public override void _Ready()
    {
        _self = GetParent().GetNodeOrNull<Combatant>("Combatant");

        var colour = Kind == StatusKind.Fortify ? Skin : Blade;

        _ringMaterial = Glow(colour, 0.55f);
        _columnMaterial = Glow(colour, 0.13f);

        // Flat tori rather than a dome: the character has to stay readable inside it, and a
        // ring on the ground never hides the fight the way a shell around the body does.
        _inner = Ring(Radius * 0.62f, 0.05f, 0.04f);
        _outer = Ring(Radius, 0.045f, 0.02f);

        _column = new MeshInstance3D
        {
            Name = "Column",
            Mesh = new CylinderMesh
            {
                TopRadius = Radius * 0.75f,
                BottomRadius = Radius * 0.55f,
                Height = 2.1f,
                RadialSegments = 20,
                Rings = 1,
                CapTop = false,
                CapBottom = false,
            },
            MaterialOverride = _columnMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 1.05f, 0),
        };

        AddChild(_inner);
        AddChild(_outer);
        AddChild(_column);

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
        Position = new Vector3(0, 0.05f + height, 0),
    };

    public override void _Process(double delta)
    {
        if (_self is null || _inner is null || _outer is null || _column is null) return;

        // Fades in and out rather than snapping, so the end of a buff is something the eye
        // catches instead of a frame where the rings were simply gone.
        var wanted = _self.IsAlive && _self.Statuses.Has(Kind) ? 1f : 0f;

        _strength = Mathf.MoveToward(_strength, wanted, (float)delta * 3.2f);

        if (_strength <= 0.01f)
        {
            Visible = false;
            return;
        }

        Visible = true;
        _spin += delta;

        // Counter-rotating, at different speeds: two rings turning together read as one ring.
        _inner.Rotation = new Vector3(0, (float)_spin * 1.6f, 0);
        _outer.Rotation = new Vector3(0, (float)-_spin * 0.9f, 0);

        var pulse = 1f + (Mathf.Sin((float)_spin * 3.0f) * 0.05f);

        _inner.Scale = Vector3.One * pulse * _strength;
        _outer.Scale = Vector3.One * (2f - pulse) * _strength;
        _column.Scale = new Vector3(pulse, 1f, pulse) * _strength;

        if (_ringMaterial is not null)
        {
            _ringMaterial.AlbedoColor = _ringMaterial.AlbedoColor with { A = 0.55f * _strength };
        }

        if (_columnMaterial is not null)
        {
            _columnMaterial.AlbedoColor = _columnMaterial.AlbedoColor with { A = 0.13f * _strength };
        }
    }
}
