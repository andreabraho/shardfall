using Godot;

namespace Kiln.Game.World;

/// <summary>
/// The ring that flashes where you clicked (MOV-04).
/// <para>
/// Small but load-bearing: under click-to-move the player needs immediate confirmation that
/// the order registered, otherwise any pathing delay reads as the game ignoring them. It
/// fires on the same frame as the click, before the character has moved at all.
/// </para>
/// </summary>
public partial class ClickMarker : Node3D
{
    private const double Duration = 0.45;

    private MeshInstance3D _ring = null!;
    private StandardMaterial3D _material = null!;
    private double _elapsed = Duration;

    [Export] public Color Color { get; set; } = new(0.55f, 0.85f, 1.0f);

    [Export] public float StartScale { get; set; } = 0.35f;

    [Export] public float EndScale { get; set; } = 1.15f;

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            AlbedoColor = Color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // Draw over the ground so it is never z-fighting or hidden by a slope.
            NoDepthTest = true,
            DisableReceiveShadows = true,
        };

        // Unrotated. A TorusMesh already lies in the XZ plane with its hole along Y, so it is
        // flat on the ground as built. This node used to turn it a quarter about X, on the
        // stated belief that a torus stands upright — which it does not, and that quarter
        // turn is what stood the marker on edge. Seen from directly behind, an upright ring
        // is indistinguishable from a flat one, so the error only showed once the camera
        // could be swung (MOV-10).
        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.45f, OuterRadius = 0.6f, RingSegments = 24 },
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        AddChild(_ring);
        Visible = false;
    }

    public void Flash(Vector3 worldPoint)
    {
        GlobalPosition = worldPoint + (Vector3.Up * 0.05f);
        _elapsed = 0;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        _elapsed += delta;

        if (_elapsed >= Duration)
        {
            Visible = false;
            return;
        }

        var t = (float)(_elapsed / Duration);

        // Expand fast then settle, fade out linearly.
        var eased = 1f - Mathf.Pow(1f - t, 3f);
        var scale = Mathf.Lerp(StartScale, EndScale, eased);

        _ring.Scale = new Vector3(scale, scale, scale);
        _material.AlbedoColor = Color with { A = 1f - t };
    }
}
