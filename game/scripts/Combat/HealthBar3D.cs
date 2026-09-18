using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// A bar floating above a combatant. Two quads and no textures, so it works with
/// placeholder art and costs nothing.
/// </summary>
/// <remarks>
/// <para><b>Why this billboards itself instead of using the material's billboard mode:</b>
/// StandardMaterial3D billboarding replaces the model-view basis in the vertex shader, which
/// discards the node's scale and makes local offsets drift as the camera rotates. The fill
/// quad depends on both — it scales to the health fraction and shifts left to drain from one
/// end — so with material billboarding the bar only slid sideways instead of emptying, which
/// reads as the value jumping around at random.</para>
/// <para>Copying the camera basis is also exactly right for a fixed-pitch camera, and it
/// cannot hit the degenerate case a look-at billboard has when the view is near vertical.</para>
/// </remarks>
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

    /// <summary>Keeps the bar visible regardless — set for the current target.</summary>
    public bool ForceVisible { get; set; }

    public override void _Ready()
    {
        var backdrop = Quad(new Color(0.05f, 0.05f, 0.07f, 0.9f), Width, Height);
        backdrop.Position = new Vector3(0, 0, -0.002f);
        AddChild(backdrop);

        _fill = Quad(FullColor, Width, Height * 0.74f);
        _fillMaterial = (StandardMaterial3D)_fill.MaterialOverride;
        AddChild(_fill);

        TopLevel = true; // position is driven manually; parent rotation must not apply
        SetFraction(1f);
    }

    private static MeshInstance3D Quad(Color color, float width, float height)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            RenderPriority = 5,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        return new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(width, height) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    public override void _Process(double delta)
    {
        var parent = GetParent<Node3D>();
        var camera = GetViewport().GetCamera3D();

        if (parent is null || camera is null) return;

        // TopLevel means we own our global transform: follow the parent's position, take
        // the camera's orientation.
        GlobalTransform = new Transform3D(camera.GlobalBasis.Orthonormalized(), parent.GlobalPosition + Offset);
    }

    /// <summary>Local offset above the body.</summary>
    [Export] public Vector3 Offset { get; set; } = new(0, 2.1f, 0);

    public void SetFraction(float fraction)
    {
        _fraction = Mathf.Clamp(fraction, 0f, 1f);

        // Scale from the left edge so the bar drains rather than shrinking from both sides.
        _fill.Scale = new Vector3(Mathf.Max(_fraction, 0.0001f), 1, 1);
        _fill.Position = new Vector3(-Width * 0.5f * (1f - _fraction), 0, 0);

        _fillMaterial.AlbedoColor = LowColor.Lerp(FullColor, _fraction);

        Visible = ForceVisible || !HideWhenFull || (_fraction < 0.999f && _fraction > 0f);
    }

    public void Refresh() => SetFraction(_fraction);
}
