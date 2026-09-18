using Kiln.Core.Foundation;

namespace Kiln.Core.Items;

public enum SocketOutcome
{
    Success,
    NoSocketsLeft,
    SocketClosed,
    SocketOccupied,
    SocketEmpty,
    NotAStone,
    CannotAfford,
    NoRoomForStone,
}

/// <summary>
/// Sockets, opened and filled deterministically (doc 02 §4.2, ITM-07).
/// </summary>
/// <remarks>
/// Three deliberate departures from the original, all aimed at the same thing — making
/// sockets something the player plays with rather than hoards:
/// <list type="bullet">
/// <item>Socket count comes from the item's rarity and is visible at drop time, so no reroll
/// loop exists to open one more.</item>
/// <item>Opening costs a craftable Boring Stone, never a random success roll.</item>
/// <item>Pulling a stone out costs a small fee and <em>returns the stone intact</em>.</item>
/// </list>
/// </remarks>
public static class SocketBench
{
    public const string BoringStoneId = "mat_boring_stone";

    /// <summary>The next socket that could be bored open, or null when they all are.</summary>
    public static int? NextClosedSocket(ItemInstance item)
    {
        for (var i = 0; i < item.Sockets.Count; i++)
        {
            if (!item.Sockets[i].IsOpen) return i;
        }

        return null;
    }

    public static SocketOutcome TryBore(ItemInstance item, IResourceStore store, string boringStoneId = BoringStoneId)
    {
        if (NextClosedSocket(item) is not { } index) return SocketOutcome.NoSocketsLeft;

        var cost = new Dictionary<string, int> { [boringStoneId] = 1 };

        if (!store.TrySpend(ItemEconomy.SocketBoreYang, cost)) return SocketOutcome.CannotAfford;

        item.Sockets[index].IsOpen = true;

        return SocketOutcome.Success;
    }

    public static SocketOutcome TrySlot(ItemInstance item, int socketIndex, string stoneId, IItemSpecs specs, IResourceStore store)
    {
        if (socketIndex < 0 || socketIndex >= item.Sockets.Count) return SocketOutcome.NoSocketsLeft;

        var socket = item.Sockets[socketIndex];

        if (!socket.IsOpen) return SocketOutcome.SocketClosed;
        if (socket.StoneId is not null) return SocketOutcome.SocketOccupied;

        // A stone is an item that grants modifiers and is worn by nothing. Checking that here
        // stops a mis-click slotting a sword into a helmet.
        if (!specs.TryGet(stoneId, out var stone) || stone.IsEquipment || stone.Grants.Count == 0)
        {
            return SocketOutcome.NotAStone;
        }

        if (!store.TrySpend(0, new Dictionary<string, int> { [stoneId] = 1 })) return SocketOutcome.CannotAfford;

        socket.StoneId = stoneId;

        return SocketOutcome.Success;
    }

    /// <summary>Pulls a stone out for a fee and hands it back to the player, undamaged.</summary>
    public static SocketOutcome TryRemove(ItemInstance item, int socketIndex, IResourceStore store)
    {
        if (socketIndex < 0 || socketIndex >= item.Sockets.Count) return SocketOutcome.NoSocketsLeft;

        var socket = item.Sockets[socketIndex];

        if (socket.StoneId is not { } stoneId) return SocketOutcome.SocketEmpty;

        if (!store.CanAfford(ItemEconomy.SocketRemoveYang, ItemSpecCosts.None)) return SocketOutcome.CannotAfford;

        // Take the stone out of the socket before granting it, and put it back if the player
        // has nowhere to hold it. Losing a stone to a full bag would be exactly the kind of
        // silent punishment this system exists to remove.
        socket.StoneId = null;

        if (!store.TryGrant(stoneId, 1))
        {
            socket.StoneId = stoneId;
            return SocketOutcome.NoRoomForStone;
        }

        store.TrySpend(ItemEconomy.SocketRemoveYang, ItemSpecCosts.None);

        return SocketOutcome.Success;
    }
}

internal static class ItemSpecCosts
{
    public static readonly IReadOnlyDictionary<string, int> None = new Dictionary<string, int>();
}

public enum RerollOutcome
{
    Success,
    NothingToReroll,
    TooManyLocked,
    CannotAfford,
    NoPool,
}

public sealed record RerollQuote(long Yang, IReadOnlyDictionary<string, int> Materials, int LockedLines, int MaxLockedLines, bool Affordable);

/// <summary>
/// Bonus rerolling with line locking (doc 02 §4.3, ITM-08).
/// </summary>
/// <remarks>
/// Locking is the whole point. An unlocked reroll is a slot machine: every pull discards the
/// good line you were trying to keep, so the player never converges and the currency feels
/// wasted. Letting one line be held — two on high-rarity items — turns the same currency into
/// a process with visible progress.
/// </remarks>
public static class RerollTable
{
    public const string MutationInkId = "mat_mutation_ink";

    /// <summary>High-rarity items may hold two lines; everything else holds one.</summary>
    public static int MaxLockedLines(ItemSpec spec) => spec.Rarity >= Rarity.Epic ? 2 : 1;

    public static RerollQuote Quote(ItemInstance item, ItemSpec spec, IResourceStore store, string inkId = MutationInkId)
    {
        var locked = item.LockedLineCount;
        var yang = (long)Math.Round(ItemEconomy.RerollYang * Math.Pow(ItemEconomy.RerollLockedLineMultiplier, locked));
        var mats = new Dictionary<string, int> { [inkId] = 1 + locked };

        return new RerollQuote(yang, mats, locked, MaxLockedLines(spec), store.CanAfford(yang, mats));
    }

    public static RerollOutcome TryReroll(
        ItemInstance item,
        ItemSpec spec,
        BonusPool? pool,
        IResourceStore store,
        DeterministicRng rng,
        string inkId = MutationInkId)
    {
        if (pool is null) return RerollOutcome.NoPool;
        if (item.Bonuses.Count == 0) return RerollOutcome.NothingToReroll;

        var locked = item.Bonuses.Where(l => l.Locked).ToList();

        if (locked.Count > MaxLockedLines(spec)) return RerollOutcome.TooManyLocked;
        if (locked.Count == item.Bonuses.Count) return RerollOutcome.NothingToReroll;

        var quote = Quote(item, spec, store, inkId);

        if (!store.TrySpend(quote.Yang, quote.Materials)) return RerollOutcome.CannotAfford;

        // The rolled count is fixed to what the item already has: a reroll changes which
        // lines you own, never how many, so it can never be a downgrade in size.
        var fixedCount = spec with { MinBonusLines = item.Bonuses.Count, MaxBonusLines = item.Bonuses.Count };

        item.SetBonuses(ItemFactory.RollLines(fixedCount, pool, rng, locked));

        return RerollOutcome.Success;
    }
}
