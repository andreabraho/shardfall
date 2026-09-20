using Godot;

namespace Kiln.Game.World;

/// <summary>
/// Where the player appears when they walk in from a particular neighbour (WLD-01).
/// </summary>
/// <remarks>
/// A map has one Player node marking where a new game starts, and that is the wrong place to
/// arrive from anywhere else: walking east out of the village and appearing in the middle of
/// the next map makes the two maps feel unconnected, however correct the graph is. One of
/// these per neighbour puts the player at the matching end of the road, facing inward.
/// </remarks>
public partial class ZoneArrival : Node3D
{
    /// <summary>The zone the player is arriving from.</summary>
    [Export] public string FromZone { get; set; } = "";

    public override void _Ready() => AddToGroup("zone_arrivals");
}
