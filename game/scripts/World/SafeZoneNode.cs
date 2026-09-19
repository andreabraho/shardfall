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
public partial class SafeZoneNode : Node3D
{
    [Export] public string RegionId { get; set; } = "";

    public override void _Ready()
    {
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
        if (GameWorld.IsLoaded) GameWorld.Safety.Remove(RegionId);
    }

    /// <summary>The declared radius, for the audit and for drawing the boundary.</summary>
    public double Radius => GameWorld.Graph.SafeRegion(RegionId)?.Radius ?? 0;
}
