namespace Kiln.Core.Items;

/// <summary>
/// Where crafting-bench operations take their payment from and hand their results back to.
/// <para>
/// The benches (anvil, socket bench, reroll table) are written against this rather than
/// against the inventory directly, so their rules can be tested with a three-line fake and
/// so a future bank or companion pouch can pay for work without any of them changing.
/// </para>
/// </summary>
public interface IResourceStore
{
    long Yang { get; }

    int CountOf(string itemId);

    /// <summary>True when the store holds the yang and every listed material.</summary>
    bool CanAfford(long yang, IReadOnlyDictionary<string, int> materials);

    /// <summary>Takes payment atomically. Returns false and takes nothing when it cannot.</summary>
    bool TrySpend(long yang, IReadOnlyDictionary<string, int> materials);

    /// <summary>Gives an item back — a stone pulled out of a socket, a refund, a vendor purchase.</summary>
    bool TryGrant(string itemId, int count);
}

/// <summary>Costs that are policy rather than content.</summary>
/// <remarks>
/// These live in code rather than a data file because they are one global tuning knob each
/// and are read by the economy simulator. If a designer ever needs to sweep them per item
/// tier they should move to <c>game/data/tables/economy.json</c>; until then a data file
/// would be ceremony around four numbers.
/// </remarks>
public static class ItemEconomy
{
    /// <summary>Yang charged on top of the Boring Stone to open one socket.</summary>
    public const long SocketBoreYang = 2_500;

    /// <summary>
    /// Fee to pull a stone back out. Deliberately small: the stone survives, because a system
    /// that destroys what you remove is a system where nobody ever experiments.
    /// </summary>
    public const long SocketRemoveYang = 800;

    /// <summary>Base yang for a bonus reroll, on top of one Mutation Ink.</summary>
    public const long RerollYang = 3_000;

    /// <summary>Multiplier applied per locked line. Locking is the convenience, so it is the cost.</summary>
    public const double RerollLockedLineMultiplier = 2.5;

    /// <summary>Vendors buy at a fraction of an item's listed value.</summary>
    public const double VendorBuybackRate = 0.25;
}
