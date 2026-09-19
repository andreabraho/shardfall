namespace Kiln.Core.World;

/// <summary>Why a fast-travel request was turned down, so the UI can say something useful.</summary>
public enum TravelRefusal
{
    None,
    UnknownShrine,

    /// <summary>The player has not stood at that shrine yet.</summary>
    NotDiscovered,

    /// <summary>Already there.</summary>
    AlreadyHere,

    /// <summary>A checkpoint, not a shrine — dungeons do not let you skip back in.</summary>
    NotATravelPoint,

    /// <summary>Fast travel is a service of the world, not something you do mid-fight.</summary>
    InCombat,

    NotEnoughYang,
}

public readonly record struct TravelQuote(bool Allowed, long Cost, TravelRefusal Refusal)
{
    public static TravelQuote Denied(TravelRefusal reason) => new(false, 0, reason);
}

/// <summary>
/// Shrines the player has found, and what moving between them costs (WLD-02, WLD-09).
/// </summary>
/// <remarks>
/// The cost exists to keep fast travel a decision rather than a reflex, and it is flat rather
/// than distance-based because a distance price punishes exactly the trip the player most
/// wants to make. It is also capped as a share of the band's income, so the fee never becomes
/// the reason a player walks: by the time walking is tedious, the fee is pocket change.
/// </remarks>
public sealed class FastTravelNetwork
{
    /// <summary>Flat fee, before the band scaling.</summary>
    public const long BaseCost = 120;

    /// <summary>Added per level of the destination band's floor.</summary>
    public const long CostPerBandLevel = 45;

    private readonly ZoneGraph _graph;
    private readonly HashSet<string> _discovered = new(StringComparer.Ordinal);

    public FastTravelNetwork(ZoneGraph graph) => _graph = graph;

    public IReadOnlyCollection<string> Discovered => _discovered;

    /// <summary>The shrine the player last touched: where death and "return" both lead.</summary>
    public string? Anchor { get; private set; }

    /// <summary>
    /// Records a shrine the player is standing at. Returns true the first time, which is what
    /// the "shrine discovered" notice keys off.
    /// </summary>
    public bool Discover(string shrineId)
    {
        Anchor = shrineId;

        return _discovered.Add(shrineId);
    }

    public bool IsDiscovered(string shrineId) => _discovered.Contains(shrineId);

    /// <summary>Everywhere the player could travel right now, in zone then name order.</summary>
    public IEnumerable<Shrine> Destinations() =>
        _discovered
            .Select(_graph.Shrine)
            .OfType<Shrine>()
            .Where(s => s.FastTravel)
            .OrderBy(s => s.Zone, StringComparer.Ordinal)
            .ThenBy(s => s.Id, StringComparer.Ordinal);

    public long CostTo(string shrineId)
    {
        if (_graph.Shrine(shrineId) is not { } shrine) return 0;

        var band = _graph[shrine.Zone]?.Band ?? new LevelBand(1, 1);

        return BaseCost + (CostPerBandLevel * Math.Max(0, band.Min - 1));
    }

    public TravelQuote Quote(string shrineId, long yang, bool inCombat)
    {
        if (_graph.Shrine(shrineId) is not { } shrine) return TravelQuote.Denied(TravelRefusal.UnknownShrine);
        if (!shrine.FastTravel) return TravelQuote.Denied(TravelRefusal.NotATravelPoint);
        if (!IsDiscovered(shrineId)) return TravelQuote.Denied(TravelRefusal.NotDiscovered);
        if (shrineId == Anchor) return TravelQuote.Denied(TravelRefusal.AlreadyHere);
        if (inCombat) return TravelQuote.Denied(TravelRefusal.InCombat);

        var cost = CostTo(shrineId);

        return yang < cost
            ? new TravelQuote(false, cost, TravelRefusal.NotEnoughYang)
            : new TravelQuote(true, cost, TravelRefusal.None);
    }

    public void Load(IEnumerable<string> discovered, string? anchor)
    {
        _discovered.Clear();

        foreach (var id in discovered) _discovered.Add(id);

        Anchor = anchor;
    }
}
