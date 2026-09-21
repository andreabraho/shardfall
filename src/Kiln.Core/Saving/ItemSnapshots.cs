using Kiln.Core.Items;

namespace Kiln.Core.Saving;

/// <summary>
/// Moves items between their live form and their saved form.
/// </summary>
/// <remarks>
/// In Core rather than beside the save service because rebuilding an item means setting the
/// things only Core may set — its upgrade level, its rolled lines, its sockets. Everywhere else
/// those are read-only, which is the whole reason an item's state can be trusted.
/// </remarks>
public static class ItemSnapshots
{
    public static SavedItem Capture(ItemInstance item, int[]? at) => new()
    {
        Uid = item.Uid,
        Def = item.DefId,
        Count = item.Count,
        Upgrade = item.UpgradeLevel,
        Failures = item.UpgradeFailures,
        Locked = item.Locked,
        At = at,
        Lines = item.Bonuses
            .Select(l => new SavedLine { Id = l.LineId, Magnitude = l.Magnitude, Locked = l.Locked })
            .ToList(),
        Sockets = item.Sockets
            .Select(s => s.IsOpen ? s.StoneId ?? "" : null)
            .ToList(),
    };

    /// <summary>
    /// Rebuilds an item. Returns null for an item whose definition no longer exists.
    /// </summary>
    /// <remarks>
    /// A line whose pool entry has since been removed is dropped rather than failing the whole
    /// load: losing one bonus on one item is a much smaller surprise than losing the save.
    /// </remarks>
    public static ItemInstance? Restore(SavedItem saved, IItemSpecs specs, IBonusPools pools)
    {
        if (!specs.TryGet(saved.Def, out var spec)) return null;

        var item = new ItemInstance(saved.Uid, saved.Def, Math.Max(1, saved.Count))
        {
            UpgradeLevel = Math.Clamp(saved.Upgrade, 0, UpgradeScaling.MaxLevel),
            UpgradeFailures = Math.Max(0, saved.Failures),
            Locked = saved.Locked,
        };

        var lines = new List<BonusLine>();

        if (spec.BonusPoolId is not null && pools.TryGet(spec.BonusPoolId, out var pool))
        {
            foreach (var line in saved.Lines)
            {
                if (pool.Find(line.Id) is { } entry)
                {
                    lines.Add(new BonusLine(line.Id, entry.Stat, line.Magnitude, line.Locked));
                }
            }
        }

        item.SetBonuses(lines);
        item.SetSocketCount(saved.Sockets.Count);

        for (var i = 0; i < saved.Sockets.Count; i++)
        {
            if (saved.Sockets[i] is not { } stone) continue;

            item.Sockets[i].IsOpen = true;
            item.Sockets[i].StoneId = stone.Length == 0 ? null : stone;
        }

        return item;
    }
}
