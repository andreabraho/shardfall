using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// Puts an imported model where the primitive it replaces used to be.
/// </summary>
/// <remarks>
/// Models arrive at whatever scale their author worked in, with their origin wherever that
/// author put it — usually at the feet. Every primitive in this game is centred on its node
/// and sized from data, and every scene was laid out against those sizes. Something has to
/// reconcile the two, and it has to be the model that moves.
/// <para>
/// Two ways to fit, because there are two kinds of thing. A wall tiles, so it must fill its
/// declared box exactly or a run of them shows gaps. A character does not tile, so it is
/// scaled evenly to the right height and left in proportion — stretching a body to a
/// declared width would be visible on the one object the player looks at most.
/// </para>
/// </remarks>
public static class ModelFit
{
    /// <summary>Fills a box exactly, distorting if it must. For anything that tiles.</summary>
    public static void Stretch(Node3D instance, Vector3 size)
    {
        var bounds = LocalBounds(instance);

        if (Degenerate(bounds)) return;

        Place(instance, bounds, new Vector3(
            size.X / bounds.Size.X,
            size.Y / bounds.Size.Y,
            size.Z / bounds.Size.Z));
    }

    /// <summary>Scales evenly until it stands the right height. For anything that does not.</summary>
    public static void ByHeight(Node3D instance, float height)
    {
        var bounds = LocalBounds(instance);

        if (Degenerate(bounds) || height <= 0.0001f) return;

        Place(instance, bounds, Vector3.One * (height / bounds.Size.Y));
    }

    private static bool Degenerate(Aabb bounds) =>
        bounds.Size.X <= 0.0001f || bounds.Size.Y <= 0.0001f || bounds.Size.Z <= 0.0001f;

    /// <summary>
    /// Scales, then slides the model so its own centre sits on the node's origin.
    /// </summary>
    /// <remarks>
    /// Matching centres rather than feet is what keeps a swap from moving anything: the
    /// primitives are centred meshes, and the nodes holding them were already positioned to
    /// put those centres in the right place.
    /// </remarks>
    private static void Place(Node3D instance, Aabb bounds, Vector3 scale)
    {
        instance.Scale = scale;
        instance.Position = -(bounds.Position + (bounds.Size * 0.5f)) * scale;
    }

    /// <summary>The combined extent of every mesh under a node, in that node's own space.</summary>
    public static Aabb LocalBounds(Node3D root)
    {
        var bounds = new Aabb();
        var found = false;

        void Walk(Node node, Transform3D relative)
        {
            if (node is MeshInstance3D { Mesh: not null } mesh)
            {
                var box = Transformed(relative, mesh.Mesh.GetAabb());

                bounds = found ? bounds.Merge(box) : box;
                found = true;
            }

            foreach (var child in node.GetChildren())
            {
                Walk(child, child is Node3D spatial ? relative * spatial.Transform : relative);
            }
        }

        Walk(root, Transform3D.Identity);

        return bounds;
    }

    /// <summary>
    /// An AABB moved into another space, by its corners.
    /// </summary>
    /// <remarks>
    /// The eight corners rather than the two extremes: under rotation the extremes of the
    /// transformed box are not the transforms of the extremes, and models are full of
    /// rotated sub-meshes.
    /// </remarks>
    private static Aabb Transformed(Transform3D transform, Aabb box)
    {
        var result = new Aabb(transform * box.Position, Vector3.Zero);

        for (var corner = 1; corner < 8; corner++)
        {
            result = result.Expand(transform * (box.Position + new Vector3(
                (corner & 1) == 0 ? 0 : box.Size.X,
                (corner & 2) == 0 ? 0 : box.Size.Y,
                (corner & 4) == 0 ? 0 : box.Size.Z)));
        }

        return result;
    }
}
