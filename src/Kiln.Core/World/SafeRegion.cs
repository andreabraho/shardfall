namespace Kiln.Core.World;

/// <summary>
/// A village: safe ground inside a map, rather than a map of its own (WLD-12).
/// </summary>
/// <remarks>
/// The genre layout this is modelled on puts the village and the creatures in the same
/// world — you stand at the gate and see what you are about to walk into. Making the village
/// a separate zone would put a loading boundary there and throw away the one view that makes
/// the arrangement work, so safety is a region, not a destination.
/// <para>
/// It holds no position for the same reason <see cref="Zone"/> does not: where the village
/// sits is level design, and lives in the scene where it can be dragged. What it is called
/// and how far it reaches is content, and lives in data where it can be checked.
/// </para>
/// </remarks>
public sealed record SafeRegion(string Id, string Name, string Zone, double Radius);

/// <summary>
/// Where the loaded scene's safe regions actually are, and the only authority on whether a
/// point is safe.
/// </summary>
/// <remarks>
/// One object answers the question for all three promises a safe zone makes — nothing spawns
/// here, nothing follows you here, nothing hurts you here. Three separate implementations of
/// "am I inside the village" would eventually disagree, and the player would find the seam by
/// dying in a place the map told them was safe.
/// </remarks>
public sealed class SafetyField
{
    /// <summary>
    /// How far a spawn field must stay clear of safe ground, in metres.
    /// </summary>
    /// <remarks>
    /// Not merely "must not overlap". A camp whose edge touches the boundary puts creatures
    /// within aggro range of someone standing safely inside, which produces the worst version
    /// of a safe zone: one that works, while something visibly tries to reach you.
    /// </remarks>
    public const double SpawnClearance = 6.0;

    private readonly List<Circle> _regions = [];

    private readonly record struct Circle(string Id, double X, double Z, double Radius);

    public int Count => _regions.Count;

    public IEnumerable<string> Ids => _regions.Select(r => r.Id);

    public void Clear() => _regions.Clear();

    /// <summary>Takes a region out, when its marker leaves the scene.</summary>
    public bool Remove(string id) => _regions.RemoveAll(r => r.Id == id) > 0;

    /// <summary>Places a declared region. The scene supplies the coordinates, data the radius.</summary>
    public void Register(string id, double x, double z, double radius)
    {
        _regions.RemoveAll(r => r.Id == id);
        _regions.Add(new Circle(id, x, z, radius));
    }

    /// <summary>The region containing this point, or null out in the world.</summary>
    public string? RegionAt(double x, double z)
    {
        foreach (var region in _regions)
        {
            if (DistanceTo(region, x, z) <= 0) return region.Id;
        }

        return null;
    }

    public bool IsSafe(double x, double z) => RegionAt(x, z) is not null;

    /// <summary>
    /// The region a circle of this radius reaches into, counting <see cref="SpawnClearance"/>,
    /// or null when it is properly clear of every one of them.
    /// </summary>
    public string? Encroaches(double x, double z, double radius, double clearance = SpawnClearance)
    {
        foreach (var region in _regions)
        {
            if (DistanceTo(region, x, z) <= radius + clearance) return region.Id;
        }

        return null;
    }

    /// <summary>Metres from the nearest safe edge; negative inside, <see cref="double.MaxValue"/> with no regions.</summary>
    public double DistanceToSafety(double x, double z)
    {
        var nearest = double.MaxValue;

        foreach (var region in _regions)
        {
            nearest = Math.Min(nearest, DistanceTo(region, x, z));
        }

        return nearest;
    }

    private static double DistanceTo(Circle region, double x, double z)
    {
        var dx = x - region.X;
        var dz = z - region.Z;

        return Math.Sqrt((dx * dx) + (dz * dz)) - region.Radius;
    }
}
