namespace Kiln.Core.World;

/// <summary>
/// Every zone and how they connect (WLD-01).
/// </summary>
/// <remarks>
/// The graph exists so the shape of the world is a thing the build can check rather than a
/// thing we remember. A zone nobody can walk to, a door to a zone that was renamed, a level
/// gap between two neighbours — all three are silent in a scene file and loud here.
/// </remarks>
public sealed class ZoneGraph
{
    private readonly Dictionary<string, Zone> _zones;
    private readonly Dictionary<string, Shrine> _shrines;

    public ZoneGraph(IEnumerable<Zone> zones, IEnumerable<Shrine> shrines)
    {
        _zones = zones.ToDictionary(z => z.Id, StringComparer.Ordinal);
        _shrines = shrines.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<Zone> Zones => _zones.Values;

    public IReadOnlyCollection<Shrine> Shrines => _shrines.Values;

    public Zone? this[string id] => _zones.GetValueOrDefault(id);

    public Shrine? Shrine(string id) => _shrines.GetValueOrDefault(id);

    /// <summary>The village. Null when content has not defined one — which the validator rejects.</summary>
    public Zone? Hub => _zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Hub);

    public IEnumerable<Shrine> ShrinesIn(string zoneId) =>
        this[zoneId] is { } zone
            ? zone.Shrines.Select(Shrine).OfType<Shrine>()
            : [];

    /// <summary>Exits the player may currently take, given their level and finished quests.</summary>
    public IEnumerable<ZoneExit> OpenExits(string zoneId, int level, IReadOnlySet<string> questsDone)
    {
        if (this[zoneId] is not { } zone) yield break;

        foreach (var exit in zone.Exits)
        {
            if (level < exit.RequiredLevel) continue;
            if (exit.RequiredQuest is { } quest && !questsDone.Contains(quest)) continue;

            yield return exit;
        }
    }

    /// <summary>Zone ids reachable from the hub by any chain of exits, ignoring the gates.</summary>
    /// <remarks>
    /// Gates are ignored on purpose: a zone behind a level requirement is still reachable, just
    /// not yet. What this is looking for is a zone reachable by nothing at all.
    /// </remarks>
    public IReadOnlySet<string> ReachableFromHub()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (Hub is not { } hub) return seen;

        var queue = new Queue<string>();
        queue.Enqueue(hub.Id);
        seen.Add(hub.Id);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (this[current] is not { } zone) continue;

            foreach (var exit in zone.Exits)
            {
                if (seen.Add(exit.To)) queue.Enqueue(exit.To);
            }
        }

        return seen;
    }

    /// <summary>Zones nothing links to. Any entry here is a content bug.</summary>
    public IReadOnlyList<string> Orphans()
    {
        var reachable = ReachableFromHub();

        return _zones.Keys.Where(id => !reachable.Contains(id)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Levels between the hub floor and the highest band top that no reachable zone covers.
    /// </summary>
    /// <remarks>
    /// A gap means the player outgrows one zone before the next one will have them, and the
    /// only cure available to them is grinding a zone that has stopped paying — the single
    /// worst failure mode of a level-banded world.
    /// </remarks>
    public IReadOnlyList<int> BandGaps()
    {
        var bands = ReachableFromHub()
            .Select(id => this[id])
            .OfType<Zone>()
            .Where(z => z.Kind != ZoneKind.Hub)
            .Select(z => z.Band)
            .ToList();

        if (bands.Count == 0) return [];

        var floor = bands.Min(b => b.Min);
        var ceiling = bands.Max(b => b.Max);
        var gaps = new List<int>();

        for (var level = floor; level <= ceiling; level++)
        {
            if (!bands.Any(b => b.Contains(level))) gaps.Add(level);
        }

        return gaps;
    }
}
