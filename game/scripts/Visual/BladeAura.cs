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
/// <para>
/// The Blade Aura skill wraps the blade the same way while it lasts (REF-21), in its rank's
/// colour and under a name of its own, so the look and the skill are both worn at once.
/// </para>
/// </remarks>
public partial class BladeAura : Node3D
{
    /// <summary>How far round the blade the points are born, in the blade's own units.</summary>
    private const float Margin = 0.035f;

    /// <summary>Puts <paramref name="aura"/> on a blade, or takes any aura off with null.</summary>
    public static void Set(MeshInstance3D? blade, CosmeticDef? aura)
    {
        if (blade is null) return;

        Remove(blade, "Aura");

        if (aura is null) return;

        // No light (2026-09-28, at your call): it tinted the steel. The points are the aura.
        Wrap(blade, new Color(aura.Color), aura.Particles, "Aura", light: 0f);
    }

    /// <summary>
    /// Wraps a blade in a stream of <paramref name="colour"/>, as a node called
    /// <paramref name="name"/>; the effect's kind picks the glints among the points.
    /// </summary>
    public static BladeAura? Wrap(MeshInstance3D blade, Color colour, string kind, string name, float light = 0.7f)
    {
        if (blade.Mesh is null) return null;

        var node = new BladeAura { Name = name };
        blade.AddChild(node);
        node.Dress(blade, colour, kind, light);

        return node;
    }

    /// <summary>Takes the stream called <paramref name="name"/> off a blade, if it has one.</summary>
    public static void Remove(MeshInstance3D blade, string name)
    {
        if (blade.GetNodeOrNull<BladeAura>(name) is not { } old) return;

        blade.RemoveChild(old);
        old.QueueFree();
    }

    private void Dress(MeshInstance3D blade, Color colour, string kind, float light)
    {
        var box = blade.Mesh!.GetAabb();

        // The blade's box, thickened on its thin sides so the points sit round the flat of the
        // blade and its edge rather than inside the steel.
        var half = box.Size * 0.5f;
        var extents = new Vector3(half.X + Margin, half.Y + Margin, half.Z + Margin);

        Position = box.GetCenter();

        // The size of a point is in the blade's space, which the model may have scaled: undone
        // here so a point is the same size on any model.
        var scale = blade.GlobalTransform.Basis.Scale;
        var shrink = 1f / Mathf.Max(0.01f, (scale.X + scale.Y + scale.Z) / 3f);

        foreach (var layer in SkinParticles.SwordAura(kind, colour, extents, shrink)) AddChild(layer);

        if (light <= 0) return;

        AddChild(new OmniLight3D
        {
            LightColor = colour,
            LightEnergy = light,
            OmniRange = 1.4f,
            ShadowEnabled = false,
        });
    }
}
