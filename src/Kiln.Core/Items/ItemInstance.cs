using Kiln.Core.Combat;

namespace Kiln.Core.Items;

/// <summary>A socket slot on an item. Closed until bored open; then it holds one stone.</summary>
public sealed class Socket
{
    public bool IsOpen { get; internal set; }

    /// <summary>Item id of the slotted stone, or null when empty.</summary>
    public string? StoneId { get; internal set; }

    public bool IsFilled => IsOpen && StoneId is not null;
}

/// <summary>
/// One particular copy of an item, with everything that was rolled or earned for it: its
/// bonus lines, its sockets, its upgrade level, and the pity counter that guarantees the
/// next upgrade will eventually land.
/// </summary>
/// <remarks>
/// Upgrade level scales only the item's <em>base</em> numbers — weapon damage, armour value,
/// the flat grants of a ring. Rolled bonus lines are untouched. That separation is what makes
/// the item readable: the player upgrades the frame, and rerolls the lines, and the two never
/// interfere.
/// </remarks>
public sealed class ItemInstance
{
    private readonly List<BonusLine> _bonuses = [];
    private readonly List<Socket> _sockets = [];

    public ItemInstance(long uid, string defId, int count = 1)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

        Uid = uid;
        DefId = defId;
        Count = count;
    }

    /// <summary>Unique within a save. Identity is per copy, so two identical swords stay distinct.</summary>
    public long Uid { get; }

    public string DefId { get; }

    /// <summary>Stack size. Always 1 for equipment.</summary>
    public int Count { get; internal set; }

    public int UpgradeLevel { get; internal set; }

    /// <summary>
    /// Consecutive failed attempts at the <em>current</em> level. Shown in the UI (FR-5.5) and
    /// reset the moment the upgrade lands.
    /// </summary>
    public int UpgradeFailures { get; internal set; }

    /// <summary>Marked by the player so auto-sort and the junk flow leave it alone.</summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Whether its born stat is drawn the fair way. Every new copy is; one loaded from a save made
    /// before 2026-10-06 is not, so it keeps the stat it was found with.
    /// </summary>
    public bool FairInnate { get; internal set; } = true;

    public IReadOnlyList<BonusLine> Bonuses => _bonuses;

    public IReadOnlyList<Socket> Sockets => _sockets;

    public int OpenSockets => _sockets.Count(s => s.IsOpen);

    public int FilledSockets => _sockets.Count(s => s.IsFilled);

    internal void SetBonuses(IEnumerable<BonusLine> lines)
    {
        _bonuses.Clear();
        _bonuses.AddRange(lines);
    }

    internal void SetSocketCount(int count)
    {
        _sockets.Clear();

        for (var i = 0; i < count; i++) _sockets.Add(new Socket());
    }

    /// <summary>Toggles the player's lock on a rolled line, for a targeted reroll.</summary>
    public void SetLineLocked(int index, bool locked)
    {
        if (index < 0 || index >= _bonuses.Count) return;

        _bonuses[index] = _bonuses[index].WithLock(locked);
    }

    public int LockedLineCount => _bonuses.Count(l => l.Locked);

    /// <summary>
    /// Everything this copy contributes to the wearer: scaled base grants, rolled lines, and
    /// whatever is slotted into its sockets.
    /// </summary>
    public StatModifiers ModifiersFor(ItemSpec spec, IItemSpecs specs)
    {
        var mods = new StatModifiers();
        var scale = UpgradeScaling.Multiplier(UpgradeLevel);

        foreach (var grant in spec.Grants)
        {
            grant.Stat.Apply(mods, ScaledGrant(grant));
        }

        if (InnateFor(spec) is { } innate) innate.Stat.Apply(mods, innate.Magnitude);

        foreach (var line in _bonuses)
        {
            line.Stat.Apply(mods, line.Magnitude);
        }

        foreach (var socket in _sockets)
        {
            if (socket.StoneId is null || !specs.TryGet(socket.StoneId, out var stone)) continue;

            foreach (var grant in stone.Grants)
            {
                grant.Stat.Apply(mods, grant.Magnitude);
            }
        }

        return mods;
    }

    /// <summary>
    /// The stat this copy was born with and what it is worth at its upgrade level, or null for an
    /// item that has none (2026-10-02).
    /// </summary>
    /// <remarks>
    /// Which of the item's stats it is comes from the copy's own uid, scrambled: random from one
    /// drop to the next, the same for the life of the copy, and nothing more to save — so a ring
    /// from a save made before rings had one has one too.
    /// <para>
    /// Scrambled in full (2026-10-06): a single multiply read from its middle bits went round in
    /// sixteens, so copies dropped a fixed number of items apart were born with the same stat and a
    /// player could find twenty earrings and never the intelligence one. A copy from an older save
    /// keeps the old draw (<see cref="FairInnate"/>), so nothing already found changes its stat.
    /// </para>
    /// </remarks>
    public BonusLine? InnateFor(ItemSpec spec)
    {
        if (spec.Innate is not { Stats.Count: > 0 } innate) return null;

        var pick = FairInnate
            ? (int)(Scramble((ulong)Uid) % (ulong)innate.Stats.Count)
            : (int)((unchecked((ulong)Uid * 0x9E3779B97F4A7C15UL) >> 33) % (ulong)innate.Stats.Count);
        var level = Math.Clamp(UpgradeLevel, 0, UpgradeScaling.MaxLevel);
        var worth = innate.Ladder.Count > 0
            ? innate.Ladder[Math.Min(level, innate.Ladder.Count - 1)]
            : innate.Magnitude * UpgradeScaling.Multiplier(level);

        return new BonusLine("innate", innate.Stats[pick], worth);
    }

    /// <summary>SplitMix64's finish: every bit of the uid reaches every bit of the result.</summary>
    private static ulong Scramble(ulong x)
    {
        unchecked
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>
    /// A base grant at this copy's upgrade level — what the item actually gives, and what its
    /// tooltip shows.
    /// </summary>
    public double ScaledGrant(BonusLine grant) => grant.Magnitude * (grant.Stat.Kind == BonusStatKind.KillRegenPct
        ? KillRegen.UpgradeMultiplier(UpgradeLevel)
        : UpgradeScaling.Multiplier(UpgradeLevel));

    public double WeaponDamageMin(ItemSpec spec) => spec.WeaponDamageMin * UpgradeScaling.Multiplier(UpgradeLevel);

    public double WeaponDamageMax(ItemSpec spec) => spec.WeaponDamageMax * UpgradeScaling.Multiplier(UpgradeLevel);

    public double ArmorValue(ItemSpec spec) => spec.ArmorValue * UpgradeScaling.Multiplier(UpgradeLevel);

    public override string ToString() => UpgradeLevel > 0 ? $"{DefId}+{UpgradeLevel}" : DefId;
}

/// <summary>
/// How much an upgrade level is worth.
/// </summary>
/// <remarks>
/// Quadratic rather than linear on purpose: +9 roughly doubles an item's base numbers, and
/// the last three levels carry a disproportionate share of that. The cost ladder is shaped
/// the same way, so the expensive end of the chase has to actually feel like something. A
/// flat +6% per level would make +9 a rounding error the player grinds for out of habit.
/// </remarks>
public static class UpgradeScaling
{
    public const int MaxLevel = 9;

    public static double Multiplier(int upgradeLevel)
    {
        var n = Math.Clamp(upgradeLevel, 0, MaxLevel);
        return 1.0 + (0.07 * n) + (0.005 * n * n);
    }

    /// <summary>The percentage a tooltip shows for the item's current level, e.g. 103 for +9.</summary>
    public static double BonusPercent(int upgradeLevel) => (Multiplier(upgradeLevel) - 1.0) * 100.0;
}
