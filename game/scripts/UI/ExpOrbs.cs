using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// The experience bar as the original draws it (REF-19): four orbs, each a quarter of the
/// level, filling from the bottom like a glass.
/// </summary>
public partial class ExpOrbs : Control
{
    public const int Count = 4;
    private const float Radius = 9f;
    private const float Gap = 6f;

    private static readonly Color Glass = new(0.10f, 0.09f, 0.07f, 0.95f);
    private static readonly Color Fill = new(0.98f, 0.80f, 0.30f);
    private static readonly Color Rim = new(0.72f, 0.58f, 0.32f);

    private float _progress;

    /// <summary>How far through the level, 0 to 1.</summary>
    public float Progress
    {
        get => _progress;
        set
        {
            var clamped = Mathf.Clamp(value, 0f, 1f);

            if (Mathf.IsEqualApprox(clamped, _progress)) return;

            _progress = clamped;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2((Count * Radius * 2) + ((Count - 1) * Gap), Radius * 2 + 2);
    }

    public override void _Draw()
    {
        for (var i = 0; i < Count; i++)
        {
            var centre = new Vector2(Radius + (i * ((Radius * 2) + Gap)), Radius + 1);
            var share = Mathf.Clamp((_progress * Count) - i, 0f, 1f);

            DrawCircle(centre, Radius, Glass);

            if (share > 0) DrawColoredPolygon(Segment(centre, share), Fill);

            // A glint, so the orb reads as glass rather than a flat disc.
            DrawCircle(centre + new Vector2(-Radius * 0.35f, -Radius * 0.4f), Radius * 0.2f, new Color(1, 1, 1, 0.18f));
            DrawArc(centre, Radius, 0, Mathf.Tau, 32, Rim, 1.5f, true);
        }
    }

    /// <summary>The part of a circle below a waterline <paramref name="share"/> of the way up.</summary>
    private static Vector2[] Segment(Vector2 centre, float share)
    {
        if (share >= 0.999f)
        {
            var whole = new Vector2[32];

            for (var i = 0; i < whole.Length; i++)
            {
                var a = Mathf.Tau * i / whole.Length;
                whole[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
            }

            return whole;
        }

        // The waterline's height, measured from the centre (screen y grows downward).
        var line = Radius - (2 * Radius * share);
        var half = Mathf.Acos(Mathf.Clamp(line / Radius, -1f, 1f));
        var points = new System.Collections.Generic.List<Vector2>();

        // From one end of the waterline round the bottom to the other.
        const int steps = 24;
        var start = (Mathf.Pi / 2) - half;
        var end = (Mathf.Pi / 2) + half;

        for (var i = 0; i <= steps; i++)
        {
            var a = start + ((end - start) * i / steps);
            points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius);
        }

        return [.. points];
    }
}
