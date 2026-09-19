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
    private readonly Dictionary<string, SafeRegion> _safe;

    public ZoneGraph(
        IEnumerable<Zone> zones,
        IEnumerable<Shrine> shrines,
        IEnumerable<SafeRegion>? safeRegions = null)
    {
        _zones = Index(zones, z => z.Id);
        _shrines = Index(shrines, s => s.Id);
        _safe = Index(safeRegions ?? [], s => s.Id);
    }

    /// <summary>
    /// Last one wins, rather than throwing on a duplicate id.
    /// </summary>
    /// <remarks>
    /// The graph is built by the validator before it has had a chance to report anything, so
    /// throwing here would replace "duplicate shrine id 'shr_x'" with a stack trace — the
    /// validator crashing on precisely the content it exists to diagnose.
    /// </remarks>
    private static Dictionary<string, T> Index<T>(IEnumerable<T> source, Func<T, string> id)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (var item in source) map[id(item)] = item;

        return map;
    }

    public IReadOnlyCollection<Zone> Zones => _zones.Values;

    public IReadOnlyCollection<Shrine> Shrines => _shrines.Values;

    public IReadOnlyCollection<SafeRegion> SafeRegions => _safe.Values;

    public Zone? this[string id] => _zones.GetValueOrDefault(id);

    public Shrine? Shrine(string id) => _shrines.GetValueOrDefault(id);

    public SafeRegion? SafeRegion(string id) => _safe.GetValueOrDefault(id);

    /// <summary>
    /// Every village. More than one is the normal case (WLD-13): the world is a chain of
    /// villages with fields between them, not a wheel around a single town.
    /// </summary>
    public IReadOnlyList<Zone> Hubs =>
        _zones.Values.Where(z => z.Kind == ZoneKind.Hub)
            .OrderBy(z => z.Band.Min).ThenBy(z => z.Id, StringComparer.Ordinal).ToList();

    /// <summary>The starting village: the lowest-banded hub. Null when content defines none.</summary>
    public Zone? Hub => Hubs.FirstOrDefault();

    public IEnumerable<SafeRegion> SafeRegionsIn(string zoneId) =>
        _safe.Values.Where(s => s.Zone == zoneId);

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

        // Seeded from the starting village only, deliberately, now that there is more than one.
        // Seeding from every hub would make a second village nothing links to look reachable —
        // and an unreachable village is precisely the mistake this walk exists to find.
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
