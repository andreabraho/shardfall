namespace Kiln.Core.Items;

/// <summary>One rollable bonus line: what it changes, how much it can roll for, how often.</summary>
public sealed record BonusPoolEntry(string Id, BonusStat Stat, double Min, double Max, double Weight);

/// <summary>
/// The slot-appropriate set of bonus lines an item rolls from. Pools are per slot rather than
/// per item so a new sword inherits a tuned, coherent set of possibilities for free.
/// </summary>
public sealed record BonusPool(string Id, IReadOnlyList<BonusPoolEntry> Lines)
{
    public BonusPoolEntry? Find(string lineId) => Lines.FirstOrDefault(l => l.Id == lineId);
}

/// <summary>
/// A rolled line on a particular item.
/// <para>
/// <see cref="Locked"/> is the heart of the reroll redesign (doc 02 §4.3): being able to keep
/// one good line turns rerolling from a slot machine into a process that converges, so the
/// player can always see themselves getting closer to the item they want.
/// </para>
/// </summary>
public readonly record struct BonusLine(string LineId, BonusStat Stat, double Magnitude, bool Locked = false)
{
    public BonusLine WithLock(bool locked) => this with { Locked = locked };

    public string Describe() => Stat.Describe(Magnitude);
}
