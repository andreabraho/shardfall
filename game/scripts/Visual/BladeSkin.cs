using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>
/// A sword skin on a blade (REF-23): a wash of its colour over the blade, a faint shine of it,
/// and the effect it gives off. The same on the sword and the great sword.
/// </summary>
/// <remarks>
/// A wash, not a new coat (2026-09-30, at your call): every sword has its own model now, and the
/// skin tints it by a quarter, so the sword still shows through. The skin's texture was a
/// recolouring of the KayKit sword and is no longer drawn on a blade.
/// <para>
/// A node on the blade, as the upgrade glow is, so taking the skin off is taking the node away:
/// the blade's own materials are never edited, only overridden per surface, and cleared again.
/// </para>
/// </remarks>
public partial class BladeSkin : Node3D
{
    /// <summary>How far a skin tints the blade's own colours toward its own.</summary>
    private const float Wash = 0.25f;

    private MeshInstance3D _blade = null!;

    /// <summary>Puts <paramref name="skin"/> on a blade, or takes any skin off with null.</summary>
    public static void Set(MeshInstance3D? blade, CosmeticDef? skin)
    {
        if (blade is null) return;

        if (blade.GetNodeOrNull<BladeSkin>("Skin") is { } old)
        {
            old.Remove();
            blade.RemoveChild(old);
            old.QueueFree();
        }

        if (skin is null) return;

        var node = new BladeSkin { Name = "Skin" };
        blade.AddChild(node);
        node.Dress(blade, skin);
    }

    private void Dress(MeshInstance3D blade, CosmeticDef skin)
    {
        _blade = blade;

        var colour = new Color(skin.Color);

        for (var s = 0; s < (blade.Mesh?.GetSurfaceCount() ?? 0); s++)
        {
            if (blade.Mesh!.SurfaceGetMaterial(s) is not BaseMaterial3D source || source.Duplicate() is not BaseMaterial3D copy) continue;

            copy.AlbedoColor = source.AlbedoColor.Lerp(colour with { A = source.AlbedoColor.A }, Wash);

            // A faint shine of the skin's colour, never over what already glows of its own.
            if (skin.Emission > 0 && !source.EmissionEnabled)
            {
                copy.EmissionEnabled = true;
                copy.Emission = colour;
                copy.EmissionEnergyMultiplier = (float)skin.Emission * 0.12f;
            }

            blade.SetSurfaceOverrideMaterial(s, copy);
        }

        if (skin.Particles.Length == 0 || blade.Mesh is null) return;

        // Along the blade: the mesh's own box, a little in from its ends.
        var box = blade.Mesh.GetAabb();

        if (SkinParticles.Build(skin.Particles, colour, box.Size * 0.45f) is { } particles)
        {
            particles.Position = box.GetCenter();
            AddChild(particles);
        }
    }

    private void Remove()
    {
        if (!IsInstanceValid(_blade) || _blade.Mesh is null) return;

        for (var s = 0; s < _blade.Mesh.GetSurfaceCount(); s++) _blade.SetSurfaceOverrideMaterial(s, null);
    }
}
