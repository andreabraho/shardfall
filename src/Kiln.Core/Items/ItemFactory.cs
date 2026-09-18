using Kiln.Core.Foundation;

namespace Kiln.Core.Items;

/// <summary>Lookup of bonus pools by id.</summary>
public interface IBonusPools
{
    bool TryGet(string id, out BonusPool pool);
}

/// <summary>
/// Rolls concrete items from their specs (ITM-02).
/// </summary>
/// <remarks>
/// Every roll goes through a <see cref="DeterministicRng"/> the caller owns, so a drop is
/// reproducible from the save seed. That matters more here than anywhere else in the game: if
/// loot were not deterministic, a player could reload for a better roll, and the whole
/// no-gambling design (doc 02 §4) would be decorative.
/// </remarks>
public sealed class ItemFactory
{
    private readonly IItemSpecs _specs;
    private readonly IBonusPools _pools;
    private long _nextUid;

    public ItemFactory(IItemSpecs specs, IBonusPools pools, long firstUid = 1)
    {
        _specs = specs;
        _pools = pools;
        _nextUid = firstUid;
    }

    /// <summary>Next id to be handed out. Saved and restored so uids never repeat across a session.</summary>
    public long NextUid
    {
        get => _nextUid;
        set => _nextUid = value;
    }

    /// <summary>A copy with no rolls: materials, stones, quest items.</summary>
    public ItemInstance CreatePlain(string defId, int count = 1) => new(_nextUid++, defId, count);

    /// <summary>A fully rolled copy: bonus lines from its pool, socket slots from its spec.</summary>
    public ItemInstance Create(string defId, DeterministicRng rng, int count = 1)
    {
        var item = new ItemInstance(_nextUid++, defId, count);

        if (!_specs.TryGet(defId, out var spec)) return item;

        item.SetSocketCount(spec.Sockets);

        if (spec.BonusPoolId is not null && _pools.TryGet(spec.BonusPoolId, out var pool))
        {
            item.SetBonuses(RollLines(spec, pool, rng));
        }

        return item;
    }

    /// <summary>
    /// Picks distinct lines, weighted, then rolls each one inside its range.
    /// </summary>
    public static List<BonusLine> RollLines(ItemSpec spec, BonusPool pool, DeterministicRng rng, IEnumerable<BonusLine>? keep = null)
    {
        var kept = keep?.ToList() ?? [];
        var target = rng.NextIntInclusive(spec.MinBonusLines, Math.Max(spec.MinBonusLines, spec.MaxBonusLines));

        // A locked line still counts against the item's line budget — locking preserves a
        // roll, it does not buy an extra one.
        target = Math.Min(target, pool.Lines.Count);
        target = Math.Max(target, kept.Count);

        var lines = new List<BonusLine>(kept);

        // Same stat twice on one item reads as a bug even when the maths is fine, so a line
        // already present (kept or just rolled) is out of the running.
        var remaining = pool.Lines.Where(e => lines.All(l => l.LineId != e.Id)).ToList();

        while (lines.Count < target && remaining.Count > 0)
        {
            var index = rng.WeightedIndex([.. remaining.Select(e => e.Weight)]);
            var entry = remaining[index];
            remaining.RemoveAt(index);

            lines.Add(new BonusLine(entry.Id, entry.Stat, RollMagnitude(entry, rng)));
        }

        return lines;
    }

    private static double RollMagnitude(BonusPoolEntry entry, DeterministicRng rng)
    {
        var integral = entry.Min % 1 == 0 && entry.Max % 1 == 0;

        return integral
            ? rng.NextIntInclusive((int)entry.Min, (int)entry.Max)
            : Math.Round(rng.NextDouble(entry.Min, entry.Max), 2);
    }
}
