using Kiln.Core.Foundation;
using Kiln.Core.Items;

namespace Kiln.Core.Economy;

/// <summary>Something on a merchant's shelf.</summary>
/// <param name="ItemId">What it is.</param>
/// <param name="Item">
/// The exact copy for sale, already rolled, so the tooltip shows the lines the player will get.
/// Null for a staple, which is minted when bought.
/// </param>
/// <param name="Price">Yang for one staple, or for the whole piece or stack otherwise.</param>
public sealed record VendorOffer(string ItemId, ItemInstance? Item, long Price)
{
    public bool IsStaple => Item is null;
}

public enum TradeResult
{
    Done,
    TooPoor,
    NoRoom,

    /// <summary>The player locked the item against exactly this.</summary>
    Locked,

    /// <summary>Nobody pays for it.</summary>
    Worthless,

    /// <summary>The offer is no longer on the shelf.</summary>
    Gone,
}

/// <summary>
/// A merchant's shelf and till (ITM-10, FR-10.3).
/// </summary>
/// <remarks>
/// Two kinds of stock. <b>Staples</b> are the materials the workbench eats, always there in
/// any quantity, because running out of iron scrap is not a decision anyone enjoys. The
/// <b>shelf</b> is a handful of rolled gear near the player's level, chosen from the save's
/// seed and the player's level — so it is the same shelf every time the game is loaded at
/// that level, and a new one each level-up. It never carries better than
/// <see cref="MaxRarity"/>: the best things in the game are found, not bought.
/// <para>
/// Sold items are kept for a while and can be bought back at what was paid for them. A
/// list-based shop is one misclick away from selling the sword you meant to keep.
/// </para>
/// </remarks>
public sealed class Vendor
{
    /// <summary>How far below the player's level the shelf still reaches.</summary>
    public const int LevelsBelow = 4;

    /// <summary>How far above. A piece the player can wear in a level is worth saving for.</summary>
    public const int LevelsAbove = 1;

    public const int BuybackSlots = 6;

    private readonly IItemSpecs _specs;
    private readonly IReadOnlyList<ItemSpec> _gear;
    private readonly List<VendorOffer> _shelf = [];
    private readonly List<VendorOffer> _buyback = [];
    private readonly List<string> _bought = [];

    public Vendor(string id, IItemSpecs specs, IEnumerable<string> staples, IEnumerable<ItemSpec> gear,
        int rotating, Rarity maxRarity = Rarity.Fine)
    {
        Id = id;
        _specs = specs;
        Rotating = rotating;
        MaxRarity = maxRarity;

        Staples = staples
            .Select(s => specs.TryGet(s, out var spec) ? new VendorOffer(s, null, BuyPrice(spec)) : null)
            .OfType<VendorOffer>()
            .ToList();

        _gear = gear.Where(g => g.IsEquipment && g.Rarity <= maxRarity && g.SellValue > 0)
            .OrderBy(g => g.Id, StringComparer.Ordinal)
            .ToList();
    }

    public string Id { get; }
    public int Rotating { get; }
    public Rarity MaxRarity { get; }

    public IReadOnlyList<VendorOffer> Staples { get; }
    public IReadOnlyList<VendorOffer> Shelf => _shelf;
    public IReadOnlyList<VendorOffer> Buyback => _buyback;

    /// <summary>Ids bought off the shelf since it was stocked. What a save needs to put the shelf back.</summary>
    public IReadOnlyList<string> Bought => _bought;

    /// <summary>The level the shelf was last stocked for, or 0 before the first visit.</summary>
    public int StockedFor { get; private set; }

    public static long BuyPrice(ItemSpec spec) => Math.Max(1, spec.SellValue);

    /// <summary>What the merchant pays for the whole stack.</summary>
    public static long SellPrice(ItemSpec spec, ItemInstance item) =>
        (long)(spec.SellValue * ItemEconomy.VendorBuybackRate) * Math.Max(1, item.Count);

    /// <summary>Gear this merchant would stock for a player of this level.</summary>
    public IEnumerable<ItemSpec> EligibleAt(int level) =>
        _gear.Where(g => g.LevelReq >= level - LevelsBelow && g.LevelReq <= level + LevelsAbove);

    /// <summary>
    /// Fills the shelf for this level. Does nothing when it is already stocked for it, so
    /// what was bought stays bought until the next level.
    /// </summary>
    public void Restock(int level, ulong seed, ItemFactory factory)
    {
        if (level == StockedFor) return;

        StockedFor = level;
        _shelf.Clear();
        _bought.Clear();

        var rng = new DeterministicRng(seed).Fork($"vendor:{Id}:{level}");
        var pool = EligibleAt(level).ToList();

        // A partial shuffle: the first few of a random permutation, all distinct.
        for (var i = 0; i < pool.Count && i < Rotating; i++)
        {
            var j = rng.NextInt(i, pool.Count);

            (pool[i], pool[j]) = (pool[j], pool[i]);

            _shelf.Add(new VendorOffer(pool[i].Id, factory.Create(pool[i].Id, rng), BuyPrice(pool[i])));
        }
    }

    /// <summary>Buys <paramref name="count"/> of a staple, or the one piece a shelf offer is.</summary>
    public TradeResult Buy(VendorOffer offer, Inventory bag, ItemFactory factory, int count = 1)
    {
        var shelved = _shelf.Contains(offer);
        var bought = _buyback.Contains(offer);

        if (!offer.IsStaple && !shelved && !bought) return TradeResult.Gone;
        if (!_specs.TryGet(offer.ItemId, out var spec)) return TradeResult.Gone;

        count = offer.IsStaple ? Math.Clamp(count, 1, Math.Max(1, spec.MaxStack)) : 1;

        var cost = offer.Price * count;

        if (bag.Yang < cost) return TradeResult.TooPoor;

        if (offer.Item is { } piece)
        {
            if (!bag.CanTake(piece.DefId, piece.Count) || !bag.TryAdd(piece)) return TradeResult.NoRoom;

            if (shelved)
            {
                _shelf.Remove(offer);
                _bought.Add(offer.ItemId);
            }

            _buyback.Remove(offer);
        }
        else
        {
            if (!bag.CanTake(offer.ItemId, count)) return TradeResult.NoRoom;

            bag.TryAdd(factory.CreatePlain(offer.ItemId, count));
        }

        bag.TrySpendYang(cost);

        return TradeResult.Done;
    }

    /// <summary>
    /// Puts a merchant back as a save left it: the shelf for that level, minus what was bought,
    /// and the buyback list.
    /// </summary>
    /// <remarks>
    /// The shelf is rebuilt from the seed rather than stored, so the same pieces with the same
    /// rolls come back. A saved purchase that no longer matches anything on it — the item data
    /// changed — is dropped, not an error.
    /// </remarks>
    public void Restore(int stockedFor, ulong seed, ItemFactory factory, IEnumerable<string> bought,
        IEnumerable<(ItemInstance Item, long Price)> buyback)
    {
        StockedFor = 0;
        Restock(stockedFor, seed, factory);

        foreach (var id in bought)
        {
            if (_shelf.FirstOrDefault(o => o.ItemId == id) is not { } offer) continue;

            _shelf.Remove(offer);
            _bought.Add(id);
        }

        _buyback.Clear();

        foreach (var (item, price) in buyback.Take(BuybackSlots))
        {
            _buyback.Add(new VendorOffer(item.DefId, item, price));
        }
    }

    /// <summary>Sells a whole stack out of the bag.</summary>
    public TradeResult Sell(ItemInstance item, Inventory bag)
    {
        if (item.Locked) return TradeResult.Locked;
        if (!_specs.TryGet(item.DefId, out var spec)) return TradeResult.Worthless;

        var price = SellPrice(spec, item);

        if (price <= 0) return TradeResult.Worthless;
        if (!bag.Remove(item)) return TradeResult.Gone;

        bag.AddYang(price);

        // Bought back as the stack it was, for what it fetched: the undo, not a trade.
        _buyback.Insert(0, new VendorOffer(item.DefId, item, price));

        if (_buyback.Count > BuybackSlots) _buyback.RemoveAt(_buyback.Count - 1);

        return TradeResult.Done;
    }
}
