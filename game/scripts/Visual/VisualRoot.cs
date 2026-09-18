using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// Every entity gets exactly one VisualRoot child, and all of its appearance hangs off it.
/// Gameplay nodes (collision, AI, stats) are siblings, never children of the visual, so
/// swapping placeholder primitives for real models can never disturb gameplay structure.
/// </summary>
/// <remarks>
/// Convention enforced by review: if gameplay code reaches through a VisualRoot to find a
/// node, that is a bug — it will break the moment art lands.
/// </remarks>
[GlobalClass]
public partial class VisualRoot : Node3D
{
    private Node3D? _current;

    /// <summary>The logical visual id from game/data/tables/visuals.json.</summary>
    [Export] public string VisualId { get; set; } = string.Empty;

    /// <summary>Optional per-instance colour override, so one primitive serves many variants.</summary>
    [Export] public string TintOverride { get; set; } = string.Empty;

    [Export] public float VisualScale { get; set; } = 1.0f;

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(VisualId))
        {
            Apply(VisualId, string.IsNullOrEmpty(TintOverride) ? null : TintOverride, VisualScale);
        }
    }

    /// <summary>Replaces the current visual. Safe to call at runtime (used by the debug tools).</summary>
    public void Apply(string visualId, string? tint = null, double scale = 1.0)
    {
        if (_current is not null)
        {
            _current.QueueFree();
            _current = null;
        }

        if (!GameContent.IsLoaded)
        {
            GD.PushWarning($"VisualRoot '{Name}': content not loaded yet; visual '{visualId}' skipped.");
            return;
        }

        if (!GameContent.Database.Visuals.TryGetValue(visualId, out var def))
        {
            // Should be impossible: the content validator fails the build on a missing
            // visual reference. If it happens, something bypassed the pipeline.
            GD.PushError($"VisualRoot '{Name}': unknown visual id '{visualId}'.");
            return;
        }

        _current = VisualRegistry.Create(def, tint, scale);
        AddChild(_current);
    }
}
