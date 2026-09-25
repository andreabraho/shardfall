using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>
/// A sword skin on a blade (REF-23): the blade's texture swapped for the skin's recolouring, a
/// shine of its colour, and the effect it gives off. The same on the sword and the great sword.
/// </summary>
/// <remarks>
/// A node on the blade, as the upgrade glow is, so taking the skin off is taking the node away:
/// the blade's own materials are never edited, only overridden per surface, and cleared again.
/// </remarks>
public partial class BladeSkin : Node3D
{
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
        var texture = ResourceLoader.Exists(skin.Texture) ? ResourceLoader.Load<Texture2D>(skin.Texture) : null;

        if (texture is null) GD.PushWarning($"[cosmetic] '{skin.Id}' texture '{skin.Texture}' is missing.");

        for (var s = 0; s < (blade.Mesh?.GetSurfaceCount() ?? 0); s++)
        {
            if (blade.Mesh!.SurfaceGetMaterial(s) is not BaseMaterial3D source || source.Duplicate() is not BaseMaterial3D copy) continue;

            if (texture is not null) copy.AlbedoTexture = texture;

            if (skin.Emission > 0)
            {
                copy.EmissionEnabled = true;
                copy.Emission = colour;
                copy.EmissionEnergyMultiplier = (float)skin.Emission;
            }

            blade.SetSurfaceOverrideMaterial(s, copy);
        }

        if (skin.Particles.Length == 0 || blade.Mesh is null) return;

        // Along the blade: the mesh's own box, a little in from its ends.
        var box = blade.Mesh.GetAabb();

        if (SkinParticles.Build(skin.Particles, colour, box.Size * 0.45f, aura: false) is { } particles)
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
