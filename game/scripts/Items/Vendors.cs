using System.Collections.Generic;
using System.Linq;
using Kiln.Core.Economy;
using Kiln.Core.Saving;
using Kiln.Data.Definitions;

namespace Kiln.Game.Items;

/// <summary>
/// The merchants' shelves, kept across borders (ITM-10).
/// </summary>
/// <remarks>
/// Lives beside the character rather than in the village scene, so a shelf the player has
/// bought from stays bought from when they come back from a hunt, and a sale can still be
/// undone after a trip through the gate. Dropped with the character on a load or a new game:
/// the shelf is rebuilt from the seed and the level, so it comes back as it was, and the
/// buyback list holds items from a game that no longer exists.
/// </remarks>
public static class Vendors
{
    private static readonly Dictionary<string, Vendor> Open = new(System.StringComparer.Ordinal);

    /// <summary>The merchant's shelf, stocked for the player's current level.</summary>
    public static Vendor For(NpcDef npc)
    {
        if (!Open.TryGetValue(npc.Id, out var vendor))
        {
            var stock = npc.Stock ?? new StockDef();

            vendor = new Vendor(npc.Id, GameItems.Catalogue, stock.Staples, GameItems.Catalogue.Specs,
                stock.Rotating, stock.MaxRarity);

            Open[npc.Id] = vendor;

            if (Pending.Remove(npc.Id, out var saved))
            {
                var catalogue = GameItems.Catalogue;
                var buyback = saved.Buyback
                    .Select((item, i) => (Item: ItemSnapshots.Restore(item, catalogue, catalogue),
                        Price: i < saved.BuybackPrices.Count ? saved.BuybackPrices[i] : 0))
                    .Where(p => p.Item is not null)
                    .Select(p => (p.Item!, p.Price));

                vendor.Restore(saved.StockedFor, GameSession.Seed, GameItems.Factory, saved.Bought, buyback);
            }
        }

        vendor.Restock(PlayerProfile.Progression.Level, GameSession.Seed, GameItems.Factory);

        return vendor;
    }

    /// <summary>Merchants from a save, taken up the first time each is opened.</summary>
    private static readonly Dictionary<string, SavedVendor> Pending = new(System.StringComparer.Ordinal);

    public static void Reset()
    {
        Open.Clear();
        Pending.Clear();
    }

    /// <summary>Every merchant the player has dealt with, for the save.</summary>
    public static Dictionary<string, SavedVendor> Capture()
    {
        var saved = new Dictionary<string, SavedVendor>(Pending, System.StringComparer.Ordinal);

        foreach (var (id, vendor) in Open)
        {
            saved[id] = new SavedVendor
            {
                StockedFor = vendor.StockedFor,
                Bought = [.. vendor.Bought],
                Buyback = [.. vendor.Buyback.Select(o => ItemSnapshots.Capture(o.Item!, null))],
                BuybackPrices = [.. vendor.Buyback.Select(o => o.Price)],
            };
        }

        return saved;
    }

    /// <summary>Holds a save's merchants until each is next opened.</summary>
    /// <remarks>
    /// Not rebuilt straight away: a shelf is stocked from the item factory, and the factory is
    /// only put back a moment later in the same load.
    /// </remarks>
    public static void Load(IReadOnlyDictionary<string, SavedVendor> saved)
    {
        Open.Clear();
        Pending.Clear();

        foreach (var (id, vendor) in saved) Pending[id] = vendor;
    }
}
