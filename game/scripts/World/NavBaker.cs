using Godot;

namespace Kiln.Game.World;

/// <summary>
/// Bakes the navigation mesh at runtime on scene load (MOV-01).
/// <para>
/// Greybox levels change constantly during Phase 1 and 6, and a stale navmesh committed
/// alongside a moved wall produces pathing bugs that look like controller bugs. Baking on
/// load costs a few milliseconds on an arena this size and removes that whole class of
/// confusion. Pre-baked meshes become worthwhile once levels are final.
/// </para>
/// </summary>
public partial class NavBaker : NavigationRegion3D
{
    /// <summary>Bake synchronously so nothing can path before the mesh exists.</summary>
    [Export] public bool BakeOnThread { get; set; }

    public override void _Ready()
    {
        if (NavigationMesh is null)
        {
            GD.PushError($"NavBaker '{Name}': no NavigationMesh assigned; nothing can path.");
            return;
        }

        var start = Time.GetTicksUsec();
        BakeNavigationMesh(BakeOnThread);

        if (!BakeOnThread)
        {
            var polygons = NavigationMesh.GetPolygonCount();
            GD.Print($"[nav] baked {polygons} polygons in {(Time.GetTicksUsec() - start) / 1000.0:F1} ms");

            if (polygons == 0)
            {
                GD.PushError(
                    $"NavBaker '{Name}': baked 0 polygons. Check that walkable geometry is a child " +
                    "of this region and that its collision layers are included in the bake settings.");
            }
        }
    }
}
