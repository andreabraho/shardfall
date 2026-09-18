using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// A bar floating above a combatant. Two quads and no textures, so it works with
/// placeholder art and costs nothing.
/// </summary>
public partial class HealthBar3D : Node3D
{
    private MeshInstance3D _fill = null!;
    private StandardMaterial3D _fillMaterial = null!;
    private float _fraction = 1f;

    [Export] public float Width { get; set; } = 1.2f;
    [Export] public float Height { get; set; } = 0.13f;
    [Export] public Color FullColor { get; set; } = new(0.45f, 0.82f, 0.38f);
    [Export] public Color LowColor { get; set; } = new(0.85f, 0.25f, 0.22f);

    /// <summary>Hide while untouched, so a quiet field is not covered in bars.</summary>
    [Export] public bool HideWhenFull { get; set; } = true;

    public override void _Ready()
    {
        var backdrop = Quad(new Color(0.05f, 0.05f, 0.07f, 0.85f), Width, Height);
        backdrop.Position = new Vector3(0, 0, -0.001f);
        AddChild(backdrop);

        _fill = Quad(FullColor, Width, Height * 0.78f);
        _fillMaterial = (StandardMaterial3D)_fill.MaterialOverride;
        AddChild(_fill);

        SetFraction(1f);
    }

    private static MeshInstance3D Quad(Color color, float width, float height)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            RenderPriority = 5,
        };

        return new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(width, height) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    public void SetFraction(float fraction)
    {
        _fraction = Mathf.Clamp(fraction, 0f, 1f);

        // Scale from the left edge so the bar drains rather than shrinking from both sides.
        _fill.Scale = new Vector3(Mathf.Max(_fraction, 0.0001f), 1, 1);
        _fill.Position = new Vector3(-Width * 0.5f * (1f - _fraction), 0, 0);

        _fillMaterial.AlbedoColor = LowColor.Lerp(FullColor, _fraction);

        if (HideWhenFull)
        {
            Visible = _fraction < 0.999f && _fraction > 0f;
        }
    }
}
