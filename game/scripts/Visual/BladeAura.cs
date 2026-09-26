using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>
/// An aura look on a blade (REF-23): a steady stream of little points of light wrapped round
/// the sword, as the original's sword aura — not a bubble round the player (2026-09-26, at
/// your call). The same on the sword and the great sword, and beside a sword skin rather than
/// in place of it.
/// </summary>
/// <remarks>
/// A node on the blade, as the skin and the upgrade glow are, so taking the aura off is taking
/// the node away. Its points live in the blade's own space: they hug it through a swing
/// instead of being left behind as a cloud.
/// </remarks>
public partial class BladeAura : Node3D
{
    /// <summary>How far round the blade the points are born, in the blade's own units.</summary>
    private const float Wrap = 0.035f;

    /// <summary>Puts <paramref name="aura"/> on a blade, or takes any aura off with null.</summary>
    public static void Set(MeshInstance3D? blade, CosmeticDef? aura)
    {
        if (blade is null) return;

        if (blade.GetNodeOrNull<BladeAura>("Aura") is { } old)
        {
            blade.RemoveChild(old);
            old.QueueFree();
        }

        if (aura is null || blade.Mesh is null) return;

        var node = new BladeAura { Name = "Aura" };
        blade.AddChild(node);
        node.Dress(blade, aura);
    }

    private void Dress(MeshInstance3D blade, CosmeticDef aura)
    {
        var colour = new Color(aura.Color);
        var box = blade.Mesh!.GetAabb();

        // The blade's box, thickened on its thin sides so the points sit round the flat of the
        // blade and its edge rather than inside the steel.
        var half = box.Size * 0.5f;
        var extents = new Vector3(half.X + Wrap, half.Y + Wrap, half.Z + Wrap);

        Position = box.GetCenter();

        // The size of a point is in the blade's space, which the model may have scaled: undone
        // here so a point is the same size on any model.
        var scale = blade.GlobalTransform.Basis.Scale;
        var shrink = 1f / Mathf.Max(0.01f, (scale.X + scale.Y + scale.Z) / 3f);

        foreach (var layer in SkinParticles.SwordAura(aura.Particles, colour, extents, shrink)) AddChild(layer);

        AddChild(new OmniLight3D
        {
            LightColor = colour,
            LightEnergy = 0.7f,
            OmniRange = 1.4f,
            ShadowEnabled = false,
        });
    }
}
