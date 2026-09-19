using Godot;

namespace Kiln.Game.World;

/// <summary>
/// A flat ring drawn in the editor to show a radius that only exists in data.
/// </summary>
/// <remarks>
/// A village boundary and a camp's footprint are numbers in JSON and nothing at all in the
/// viewport, which makes laying out a map a matter of arithmetic between drags. These rings
/// are editor-only: they are never added while the game is running, and they carry no owner,
/// so they are not saved into the scene either.
/// </remarks>
public static class EditorRing
{
    public const string NodeName = "EditorRadius";

    /// <summary>
    /// Draws, or redraws, the ring under <paramref name="parent"/>. Does nothing at run time.
    /// </summary>
    /// <param name="thickness">Wider reads as a boundary, narrower as a footprint.</param>
    public static void Show(Node3D parent, double radius, Color colour, float thickness = 0.25f)
    {
        if (!Engine.IsEditorHint()) return;

        parent.GetNodeOrNull<Node>(NodeName)?.QueueFree();

        if (radius <= 0) return;

        var ring = new MeshInstance3D
        {
            Name = NodeName,
            Mesh = new TorusMesh
            {
                InnerRadius = Mathf.Max(0.05f, (float)radius - thickness),
                OuterRadius = (float)radius,
                RingSegments = 64,
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,

                // Drawn through terrain on purpose: a boundary you can only see from directly
                // above is no use while placing something against it.
                NoDepthTest = true,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        parent.AddChild(ring);
    }
}
