using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// The shine a blade takes on from +7, as in the original (REF-22): faint at +7, brighter and
/// lit at +8, gold and pulsing at +9.
/// </summary>
/// <remarks>
/// A shell a little larger than the blade, drawn additively over it, so the sword itself
/// keeps its look and the light seems to come off it.
/// </remarks>
public partial class WeaponGlow : Node
{
    private MeshInstance3D _blade = null!;
    private StandardMaterial3D _shell = null!;
    private OmniLight3D? _light;
    private float _alpha;
    private double _time;

    /// <summary>
    /// Puts the glow for an upgrade level on a blade, or takes it off below +7. A sword skin's
    /// colour (REF-23), when given, replaces the blue and the gold.
    /// </summary>
    public static void Set(MeshInstance3D? blade, int upgrade, Color? tint = null)
    {
        if (blade is null) return;

        blade.GetNodeOrNull<WeaponGlow>("Glow")?.Remove();

        if (upgrade < 7) return;

        var glow = new WeaponGlow { Name = "Glow" };
        blade.AddChild(glow);
        glow.Light(blade, upgrade, tint);
    }

    private void Light(MeshInstance3D blade, int upgrade, Color? tint)
    {
        _blade = blade;

        var (colour, alpha, grow, light) = upgrade switch
        {
            7 => (new Color(0.55f, 0.75f, 1f), 0.22f, 0.012f, 0f),
            8 => (new Color(0.6f, 0.82f, 1f), 0.36f, 0.018f, 0.5f),
            _ => (new Color(1f, 0.84f, 0.45f), 0.5f, 0.024f, 0.9f),
        };

        if (tint is { } skin) colour = skin;

        _alpha = alpha;
        _shell = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoColor = colour with { A = alpha },
            Grow = true,
            GrowAmount = grow,
            CullMode = BaseMaterial3D.CullModeEnum.Front,
        };

        _blade.MaterialOverlay = _shell;

        if (light <= 0) return;

        _light = new OmniLight3D
        {
            LightColor = colour,
            LightEnergy = light,
            OmniRange = 1.6f + light,
            ShadowEnabled = false,
        };

        _blade.AddChild(_light);
    }

    private void Remove()
    {
        if (IsInstanceValid(_blade) && _blade.MaterialOverlay == _shell) _blade.MaterialOverlay = null;

        _light?.QueueFree();
        QueueFree();
    }

    public override void _Process(double delta)
    {
        // A slow breath, so it reads as light rather than paint.
        _time += delta;
        _shell.AlbedoColor = _shell.AlbedoColor with { A = _alpha * (0.75f + (0.25f * Mathf.Sin((float)_time * 2.4f))) };
    }
}
