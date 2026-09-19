using Godot;

namespace Kiln.Game.World;

/// <summary>
/// Marks where a village stands (WLD-12). One exported id; the radius comes from data.
/// </summary>
/// <remarks>
/// The node carries no behaviour of its own on purpose. Safety is enforced at the three
/// places it has to be — spawning, aggro and damage — each of which asks
/// <see cref="GameWorld.Safety"/>. A marker that also policed its own boundary with an
/// <c>Area3D</c> would be a fourth answer to the same question, and the one most likely to
/// miss something that never raised a body-entered signal.
/// </remarks>
[Tool]
public partial class SafeZoneNode : Node3D
{
    private string _regionId = "";

    [Export]
    public string RegionId
    {
        get => _regionId;
        set { _regionId = value; if (IsNodeReady()) DrawBoundary(); }
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            DrawBoundary();
            return;
        }

        if (!GameWorld.IsLoaded)
        {
            GD.PushError($"[safe] '{RegionId}' loaded before GameWorld.Load() ran.");
            return;
        }

        var region = GameWorld.Graph.SafeRegion(RegionId);

        if (region is null)
        {
            GD.PushError($"[safe] no safe region '{RegionId}' in content — this ground is NOT safe.");
            return;
        }

        AddToGroup("safe_zones");
        GameWorld.Safety.Register(RegionId, GlobalPosition.X, GlobalPosition.Z, region.Radius);
    }

    /// <summary>
    /// Takes the village out with the scene.
    /// </summary>
    /// <remarks>
    /// Leaving it behind would be the worst of the failures available here: invisible safe
    /// ground at the same coordinates in the next map, where nothing can hurt the player and
    /// nothing explains why.
    /// </remarks>
    public override void _ExitTree()
    {
        if (!Engine.IsEditorHint() && GameWorld.IsLoaded) GameWorld.Safety.Remove(_regionId);
    }

    /// <summary>The declared radius, for the audit and for drawing the boundary.</summary>
    public double Radius => Lookup()?.Radius ?? 0;

    private Kiln.Core.World.SafeRegion? Lookup()
    {
        if (!GameContent.IsLoaded && !GameContent.EnsureLoadedForEditor()) return null;

        // In the editor there is no GameWorld, so the region is read straight from content.
        return GameWorld.IsLoaded
            ? GameWorld.Graph.SafeRegion(_regionId)
            : Region(_regionId);
    }

    private static Kiln.Core.World.SafeRegion? Region(string id)
    {
        foreach (var zone in GameContent.Database.Zones.Values)
        {
            foreach (var region in zone.SafeRegions)
            {
                if (region.Id == id) return new Kiln.Core.World.SafeRegion(region.Id, region.Name, zone.Id, region.Radius);
            }
        }

        return null;
    }

    /// <summary>
    /// Shows how far the village reaches while it is being placed. A village is a radius in
    /// JSON and nothing in the viewport, and its walls are only decoration around it — so
    /// without this the two are aligned by arithmetic and hope.
    /// </summary>
    private void DrawBoundary() =>
        EditorRing.Show(this, Radius, new Color(1f, 0.85f, 0.54f, 0.55f), thickness: 0.4f);
}
